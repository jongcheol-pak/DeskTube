using DeskTube.Models;

namespace DeskTube.Services;

/// <summary>
/// 설정·플레이리스트 영속화 계약 (PRD FR-14).
/// 경로 주입형 구현(JsonStateStore)으로 테스트에서 임시 폴더 사용 가능 (plan T2 Halt Forecast).
/// </summary>
public interface IStateStore
{
    Task<AppSettings> LoadSettingsAsync();

    Task<Result> SaveSettingsAsync(AppSettings settings);

    Task<List<Playlist>> LoadPlaylistsAsync();

    Task<Result> SavePlaylistsAsync(List<Playlist> playlists);
}
