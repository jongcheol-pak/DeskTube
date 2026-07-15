using DeskTube.Models;

namespace DeskTube.Services;

/// <summary>
/// 플레이리스트 CRUD + 상한 강제 (PRD FR-6: 리스트 100개 / 리스트당 항목 1000개).
/// 상태 접근은 UI 스레드(DispatcherQueue) 단일 직렬화 전제 — 내부 잠금 없음 (plan D10).
/// 변경 후 영속화는 호출자가 SaveAsync로 수행한다.
/// </summary>
public sealed class PlaylistLibrary
{
    public const int MaxPlaylists = 100;
    public const int MaxItemsPerPlaylist = 1000;

    private readonly IStateStore _store;
    private readonly List<Playlist> _playlists = [];

    public PlaylistLibrary(IStateStore store)
    {
        _store = store;
    }

    public IReadOnlyList<Playlist> Playlists => _playlists;

    /// <summary>저장소에서 목록을 로드한다 (앱 시작 시 1회).</summary>
    public async Task InitializeAsync()
    {
        _playlists.Clear();
        _playlists.AddRange(await _store.LoadPlaylistsAsync());
    }

    public Task<Result> SaveAsync() => _store.SavePlaylistsAsync(_playlists);

    public Result<Playlist> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Playlist>.Fail(ErrorCode.InvalidInput, "플레이리스트 이름이 비어 있습니다.");
        }

        if (_playlists.Count >= MaxPlaylists)
        {
            return Result<Playlist>.Fail(ErrorCode.LimitExceeded, $"플레이리스트는 최대 {MaxPlaylists}개까지 만들 수 있습니다.");
        }

        var playlist = new Playlist { Name = name.Trim() };
        _playlists.Add(playlist);
        return Result<Playlist>.Ok(playlist);
    }

    public Result Rename(Guid id, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return Result.Fail(ErrorCode.InvalidInput, "플레이리스트 이름이 비어 있습니다.");
        }

        var playlist = Find(id);
        if (playlist is null)
        {
            return Result.Fail(ErrorCode.NotFound);
        }

        playlist.Name = newName.Trim();
        return Result.Ok();
    }

    public Result Delete(Guid id)
    {
        var playlist = Find(id);
        if (playlist is null)
        {
            return Result.Fail(ErrorCode.NotFound);
        }

        _playlists.Remove(playlist);
        return Result.Ok();
    }

    public Result AddItem(Guid playlistId, string url, string videoId)
    {
        var playlist = Find(playlistId);
        if (playlist is null)
        {
            return Result.Fail(ErrorCode.NotFound);
        }

        if (playlist.Items.Count >= MaxItemsPerPlaylist)
        {
            return Result.Fail(ErrorCode.LimitExceeded, $"한 플레이리스트에는 최대 {MaxItemsPerPlaylist}개까지 담을 수 있습니다.");
        }

        playlist.Items.Add(new PlaylistItem { Url = url, VideoId = videoId });
        return Result.Ok();
    }

    public Result RemoveItem(Guid playlistId, Guid itemId)
    {
        var playlist = Find(playlistId);
        if (playlist is null)
        {
            return Result.Fail(ErrorCode.NotFound);
        }

        var removed = playlist.Items.RemoveAll(i => i.Id == itemId);
        return removed > 0 ? Result.Ok() : Result.Fail(ErrorCode.NotFound);
    }

    public Result MoveItem(Guid playlistId, int fromIndex, int toIndex)
    {
        var playlist = Find(playlistId);
        if (playlist is null)
        {
            return Result.Fail(ErrorCode.NotFound);
        }

        if (fromIndex < 0 || fromIndex >= playlist.Items.Count ||
            toIndex < 0 || toIndex >= playlist.Items.Count)
        {
            return Result.Fail(ErrorCode.InvalidInput, "이동 위치가 목록 범위를 벗어났습니다.");
        }

        var item = playlist.Items[fromIndex];
        playlist.Items.RemoveAt(fromIndex);
        playlist.Items.Insert(toIndex, item);
        return Result.Ok();
    }

    private Playlist? Find(Guid id) => _playlists.FirstOrDefault(p => p.Id == id);
}
