using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DeskTube.Models;

namespace DeskTube.Services;

/// <summary>
/// JSON 파일 영속화 (plan D5) — settings.json / playlists.json 2파일.
/// 쓰기는 임시 파일 후 원자적 교체(손상 방지), 손상 파일은 .bak으로 옮기고 기본값 반환(조용한 소실 금지).
/// </summary>
public sealed class JsonStateStore : IStateStore
{
    private const string SettingsFileName = "settings.json";
    private const string PlaylistsFileName = "playlists.json";

    private readonly string _dataDirectory;

    /// <param name="dataDirectory">저장 폴더 — 앱은 ApplicationData.LocalFolder, 테스트는 임시 폴더 주입.</param>
    public JsonStateStore(string dataDirectory)
    {
        _dataDirectory = dataDirectory;
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        var settings = await LoadAsync(SettingsFileName, StateJsonContext.Default.AppSettings)
                       ?? new AppSettings();
        settings.Normalize();
        return settings;
    }

    public Task<Result> SaveSettingsAsync(AppSettings settings) =>
        SaveAsync(SettingsFileName, settings, StateJsonContext.Default.AppSettings);

    public async Task<List<Playlist>> LoadPlaylistsAsync() =>
        await LoadAsync(PlaylistsFileName, StateJsonContext.Default.ListPlaylist) ?? [];

    public Task<Result> SavePlaylistsAsync(List<Playlist> playlists) =>
        SaveAsync(PlaylistsFileName, playlists, StateJsonContext.Default.ListPlaylist);

    private async Task<T?> LoadAsync<T>(string fileName, JsonTypeInfo<T> typeInfo) where T : class
    {
        var path = Path.Combine(_dataDirectory, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync(stream, typeInfo);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // 손상 파일은 .bak으로 보존 후 기본값으로 시작 (plan T2 Edge Case — 조용한 소실 금지)
            AppLog.Write($"상태 파일 손상 감지({fileName}): {ex.GetType().Name} — .bak으로 보존 후 기본값 사용");
            TryBackupCorruptFile(path);
            return null;
        }
    }

    private async Task<Result> SaveAsync<T>(string fileName, T value, JsonTypeInfo<T> typeInfo)
    {
        var path = Path.Combine(_dataDirectory, fileName);
        var tempPath = path + ".tmp";

        try
        {
            Directory.CreateDirectory(_dataDirectory);
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, value, typeInfo);
            }

            // 원자적 교체 — 쓰기 도중 실패해도 기존 파일은 유지된다 (plan T2 Edge Case)
            File.Move(tempPath, path, overwrite: true);
            return Result.Ok();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"상태 저장 실패({fileName}): {ex.GetType().Name}");
            return Result.Fail(ErrorCode.StorageFailure, ex.Message);
        }
    }

    private static void TryBackupCorruptFile(string path)
    {
        try
        {
            File.Move(path, path + ".bak", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 백업조차 실패하면 원본을 그대로 둔다 (다음 저장이 덮어씀)
        }
    }
}

/// <summary>System.Text.Json 소스 생성 컨텍스트 (plan D5 — 리플렉션 회피).</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(List<Playlist>))]
internal sealed partial class StateJsonContext : JsonSerializerContext;
