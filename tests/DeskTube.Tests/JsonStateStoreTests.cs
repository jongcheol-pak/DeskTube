using DeskTube.Models;
using DeskTube.Services;
using Xunit;

namespace DeskTube.Tests;

/// <summary>JsonStateStore 왕복·손상 복구 검증 (plan T2 acceptance — 임시 폴더 주입).</summary>
public sealed class JsonStateStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonStateStore _store;

    public JsonStateStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DeskTubeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _store = new JsonStateStore(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // 임시 폴더 정리 실패는 테스트 결과에 영향 없음
        }
    }

    [Fact]
    public async Task 설정_저장_후_재로드하면_왕복_일치한다()
    {
        var settings = new AppSettings
        {
            Volume = 73,
            IsMuted = true,
            Mode = PlaybackMode.Shuffle,
            SelectedMonitorIds = ["MON-A", "MON-B"],
            AudioMonitorId = "MON-B",
            QualityScaleHeight = 720,
            LastPlaylistId = Guid.NewGuid(),
            PauseOnFullscreen = false,
            Language = "ko",
        };

        var saved = await _store.SaveSettingsAsync(settings);
        var loaded = await _store.LoadSettingsAsync();

        Assert.True(saved.IsSuccess);
        Assert.Equal(settings.Volume, loaded.Volume);
        Assert.Equal(settings.IsMuted, loaded.IsMuted);
        Assert.Equal(settings.Mode, loaded.Mode);
        Assert.Equal(settings.SelectedMonitorIds, loaded.SelectedMonitorIds);
        Assert.Equal(settings.AudioMonitorId, loaded.AudioMonitorId);
        Assert.Equal(settings.QualityScaleHeight, loaded.QualityScaleHeight);
        Assert.Equal(settings.LastPlaylistId, loaded.LastPlaylistId);
        Assert.Equal(settings.PauseOnFullscreen, loaded.PauseOnFullscreen);
        Assert.Equal(settings.Language, loaded.Language);
    }

    [Fact]
    public async Task 플레이리스트_저장_후_재로드하면_왕복_일치한다()
    {
        var playlist = new Playlist { Name = "출근길" };
        playlist.Items.Add(new PlaylistItem { Url = "https://youtu.be/abc123def45", VideoId = "abc123def45" });

        var saved = await _store.SavePlaylistsAsync([playlist]);
        var loaded = await _store.LoadPlaylistsAsync();

        Assert.True(saved.IsSuccess);
        var restored = Assert.Single(loaded);
        Assert.Equal(playlist.Id, restored.Id);
        Assert.Equal(playlist.Name, restored.Name);
        var item = Assert.Single(restored.Items);
        Assert.Equal("abc123def45", item.VideoId);
    }

    [Fact]
    public async Task 파일이_없으면_기본값을_반환한다()
    {
        var settings = await _store.LoadSettingsAsync();
        var playlists = await _store.LoadPlaylistsAsync();

        Assert.Equal(50, settings.Volume);
        Assert.Equal(PlaybackMode.Sequential, settings.Mode);
        Assert.Empty(playlists);
    }

    [Fact]
    public async Task 손상된_JSON은_bak으로_보존하고_기본값으로_시작한다()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        await File.WriteAllTextAsync(path, "{ 깨진 JSON !!!");

        var loaded = await _store.LoadSettingsAsync();

        Assert.Equal(50, loaded.Volume); // 기본값
        Assert.True(File.Exists(path + ".bak")); // 원본 보존
        Assert.False(File.Exists(path)); // 손상 파일은 이동됨
    }

    [Fact]
    public async Task 범위_밖_볼륨은_로드_시_보정된다()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        await File.WriteAllTextAsync(path, """{ "SchemaVersion": 1, "Volume": 250 }""");

        var loaded = await _store.LoadSettingsAsync();

        Assert.Equal(100, loaded.Volume);
    }
}
