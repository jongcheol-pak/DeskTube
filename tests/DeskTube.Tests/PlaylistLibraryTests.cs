using DeskTube.Models;
using DeskTube.Services;
using Xunit;

namespace DeskTube.Tests;

/// <summary>PlaylistLibrary CRUD·상한 검증 (plan T2 acceptance — PRD FR-6).</summary>
public sealed class PlaylistLibraryTests
{
    /// <summary>영속화 없이 메모리에서만 도는 가짜 저장소.</summary>
    private sealed class FakeStore : IStateStore
    {
        public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(new AppSettings());
        public Task<Result> SaveSettingsAsync(AppSettings settings) => Task.FromResult(Result.Ok());
        public Task<List<Playlist>> LoadPlaylistsAsync() => Task.FromResult(new List<Playlist>());
        public Task<Result> SavePlaylistsAsync(List<Playlist> playlists) => Task.FromResult(Result.Ok());
    }

    private static PlaylistLibrary CreateLibrary() => new(new FakeStore());

    [Fact]
    public void 플레이리스트를_생성_이름변경_삭제할_수_있다()
    {
        var library = CreateLibrary();

        var created = library.Create("출근길");
        Assert.True(created.IsSuccess);
        Assert.Single(library.Playlists);

        var renamed = library.Rename(created.Value!.Id, "퇴근길");
        Assert.True(renamed.IsSuccess);
        Assert.Equal("퇴근길", library.Playlists[0].Name);

        var deleted = library.Delete(created.Value!.Id);
        Assert.True(deleted.IsSuccess);
        Assert.Empty(library.Playlists);
    }

    [Fact]
    public void 빈_이름은_거부된다()
    {
        var library = CreateLibrary();

        Assert.Equal(ErrorCode.InvalidInput, library.Create("  ").Code);

        var created = library.Create("정상");
        Assert.Equal(ErrorCode.InvalidInput, library.Rename(created.Value!.Id, "").Code);
    }

    [Fact]
    public void 이름_앞뒤_공백은_제거된다()
    {
        var library = CreateLibrary();

        var created = library.Create("  바다 영상  ");

        Assert.Equal("바다 영상", created.Value!.Name);
    }

    [Fact]
    public void 중복_이름은_허용된다()
    {
        var library = CreateLibrary();

        Assert.True(library.Create("같은 이름").IsSuccess);
        Assert.True(library.Create("같은 이름").IsSuccess);
        Assert.Equal(2, library.Playlists.Count);
    }

    [Fact]
    public void 플레이리스트_101번째_생성은_거부된다()
    {
        var library = CreateLibrary();
        for (var i = 0; i < PlaylistLibrary.MaxPlaylists; i++)
        {
            Assert.True(library.Create($"리스트 {i}").IsSuccess);
        }

        var overflow = library.Create("초과");

        Assert.False(overflow.IsSuccess);
        Assert.Equal(ErrorCode.LimitExceeded, overflow.Code);
        Assert.Equal(PlaylistLibrary.MaxPlaylists, library.Playlists.Count);
    }

    [Fact]
    public void 항목_1001번째_추가는_거부된다()
    {
        var library = CreateLibrary();
        var playlist = library.Create("대용량").Value!;
        for (var i = 0; i < PlaylistLibrary.MaxItemsPerPlaylist; i++)
        {
            Assert.True(library.AddItem(playlist.Id, $"url{i}", $"video{i:D5}").IsSuccess);
        }

        var overflow = library.AddItem(playlist.Id, "초과", "overflow0000");

        Assert.False(overflow.IsSuccess);
        Assert.Equal(ErrorCode.LimitExceeded, overflow.Code);
        Assert.Equal(PlaylistLibrary.MaxItemsPerPlaylist, playlist.Items.Count);
    }

    [Fact]
    public void 항목_추가_삭제_이동이_동작한다()
    {
        var library = CreateLibrary();
        var playlist = library.Create("편집").Value!;
        library.AddItem(playlist.Id, "url-a", "aaaaaaaaaaa");
        library.AddItem(playlist.Id, "url-b", "bbbbbbbbbbb");
        library.AddItem(playlist.Id, "url-c", "ccccccccccc");

        var moved = library.MoveItem(playlist.Id, 2, 0);
        Assert.True(moved.IsSuccess);
        Assert.Equal("ccccccccccc", playlist.Items[0].VideoId);

        var removed = library.RemoveItem(playlist.Id, playlist.Items[1].Id);
        Assert.True(removed.IsSuccess);
        Assert.Equal(2, playlist.Items.Count);
    }

    [Fact]
    public void 범위_밖_이동은_거부된다()
    {
        var library = CreateLibrary();
        var playlist = library.Create("이동").Value!;
        library.AddItem(playlist.Id, "url-a", "aaaaaaaaaaa");

        Assert.Equal(ErrorCode.InvalidInput, library.MoveItem(playlist.Id, 0, 5).Code);
        Assert.Equal(ErrorCode.InvalidInput, library.MoveItem(playlist.Id, -1, 0).Code);
    }

    [Fact]
    public void 없는_ID_참조는_NotFound를_반환한다()
    {
        var library = CreateLibrary();

        Assert.Equal(ErrorCode.NotFound, library.Rename(Guid.NewGuid(), "이름").Code);
        Assert.Equal(ErrorCode.NotFound, library.Delete(Guid.NewGuid()).Code);
        Assert.Equal(ErrorCode.NotFound, library.AddItem(Guid.NewGuid(), "url", "videoid0000").Code);
    }

    [Fact]
    public void 빠른재생_이름이_다르면_현재_언어_이름으로_동기화하고_true를_반환한다()
    {
        var library = CreateLibrary();
        var quick = library.Create("빠른 재생").Value!;

        var changed = library.SyncQuickPlaylistName(quick.Id, "Quick play");

        Assert.True(changed);
        Assert.Equal("Quick play", library.Playlists[0].Name);
    }

    [Fact]
    public void 빠른재생_ID가_null이면_변경없이_false를_반환한다()
    {
        var library = CreateLibrary();
        library.Create("빠른 재생");

        var changed = library.SyncQuickPlaylistName(null, "Quick play");

        Assert.False(changed);
        Assert.Equal("빠른 재생", library.Playlists[0].Name);
    }

    [Fact]
    public void 빠른재생_ID가_가리키는_리스트가_없으면_false를_반환한다()
    {
        var library = CreateLibrary();

        var changed = library.SyncQuickPlaylistName(Guid.NewGuid(), "Quick play");

        Assert.False(changed);
    }

    [Fact]
    public void 빠른재생_이름이_이미_같으면_변경없이_false를_반환한다()
    {
        var library = CreateLibrary();
        var quick = library.Create("Quick play").Value!;

        var changed = library.SyncQuickPlaylistName(quick.Id, "Quick play");

        Assert.False(changed);
        Assert.Equal("Quick play", library.Playlists[0].Name);
    }

    [Fact]
    public async Task 라이브러리를_저장하고_다시_로드하면_동일_목록이_복원된다()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DeskTubeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var store = new JsonStateStore(tempDir);
            var library = new PlaylistLibrary(store);
            var playlist = library.Create("영속화").Value!;
            library.AddItem(playlist.Id, "https://youtu.be/abc123def45", "abc123def45");
            var saved = await library.SaveAsync();
            Assert.True(saved.IsSuccess);

            var reloaded = new PlaylistLibrary(store);
            await reloaded.InitializeAsync();

            var restored = Assert.Single(reloaded.Playlists);
            Assert.Equal(playlist.Id, restored.Id);
            Assert.Equal("abc123def45", Assert.Single(restored.Items).VideoId);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 정리 실패 무시 (프로덕션 코드와 동일 예외 집합)
            }
        }
    }
}
