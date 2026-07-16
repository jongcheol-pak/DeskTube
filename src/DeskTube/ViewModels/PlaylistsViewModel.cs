using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskTube.Models;
using DeskTube.Services;
using Microsoft.UI.Xaml.Controls;

namespace DeskTube.ViewModels;

/// <summary>좌측 목록의 플레이리스트 1건 (이름 변경 반영용 래퍼).</summary>
public partial class PlaylistEntry : ObservableObject
{
    public PlaylistEntry(Guid id, string name, int itemCount)
    {
        Id = id;
        Name = name;
        ItemCount = itemCount;
    }

    public Guid Id { get; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial int ItemCount { get; set; }
}

/// <summary>
/// 플레이리스트 관리 (PRD FR-6 UI, plan T3).
/// 좌측 리스트 CRUD + 우측 항목 추가/삭제/이동 + 재생. 변경 즉시 저장하고,
/// 재생 중인 리스트 변경은 Coordinator.NotifyPlaylistChangedAsync로 큐에 반영한다.
/// </summary>
public partial class PlaylistsViewModel : ObservableObject
{
    private AppServices? _services;

    /// <summary>드래그 정렬 동기화 중 재진입 억제 (뷰 컬렉션 재구성 시).</summary>
    private bool _syncingItems;

    public PlaylistsViewModel()
    {
        NewUrl = string.Empty;
    }

    public ObservableCollection<PlaylistEntry> Playlists { get; } = [];

    /// <summary>선택된 리스트의 항목 (드래그 정렬을 위해 뷰 전용 컬렉션 — 모델과 순서 동기화).</summary>
    public ObservableCollection<PlaylistItem> Items { get; } = [];

    [ObservableProperty]
    public partial bool IsReady { get; set; }

    [ObservableProperty]
    public partial PlaylistEntry? SelectedPlaylist { get; set; }

    [ObservableProperty]
    public partial string NewUrl { get; set; }

    [ObservableProperty]
    public partial bool CanAddItem { get; set; }

    [ObservableProperty]
    public partial bool HasSelection { get; set; }

    [ObservableProperty]
    public partial string? NoticeMessage { get; set; }

    [ObservableProperty]
    public partial bool IsNoticeOpen { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity NoticeSeverity { get; set; }

    /// <summary>페이지 진입 시 호출 (SettingsViewModel과 동일한 지연 초기화 패턴).</summary>
    public void Load()
    {
        if (App.Services is null)
        {
            App.ServicesInitialized += OnServicesInitialized;
            return;
        }

        Populate(App.Services);
    }

    public void Detach() => App.ServicesInitialized -= OnServicesInitialized;

    private void OnServicesInitialized(object? sender, EventArgs e)
    {
        App.ServicesInitialized -= OnServicesInitialized;
        Populate(App.Services!);
    }

    private void Populate(AppServices services)
    {
        _services = services;
        Playlists.Clear();
        foreach (var playlist in services.Library.Playlists)
        {
            Playlists.Add(new PlaylistEntry(playlist.Id, playlist.Name, playlist.Items.Count));
        }

        IsReady = true;
    }

    partial void OnSelectedPlaylistChanged(PlaylistEntry? value) => RefreshItems();

    /// <summary>우측 항목 컬렉션을 모델에서 다시 채운다 (선택 변경·항목 조작 후).</summary>
    private void RefreshItems()
    {
        _syncingItems = true;
        try
        {
            Items.Clear();
            var playlist = FindSelected();
            if (playlist is not null)
            {
                foreach (var item in playlist.Items)
                {
                    Items.Add(item);
                }
            }

            HasSelection = playlist is not null;
            CanAddItem = playlist is not null && playlist.Items.Count < PlaylistLibrary.MaxItemsPerPlaylist;
            if (playlist is not null && playlist.Items.Count >= PlaylistLibrary.MaxItemsPerPlaylist)
            {
                ShowNotice(
                    string.Format(Loc.Get("Playlists_ItemLimit"), PlaylistLibrary.MaxItemsPerPlaylist),
                    InfoBarSeverity.Warning);
            }
        }
        finally
        {
            _syncingItems = false;
        }
    }

    private Playlist? FindSelected() =>
        SelectedPlaylist is null ? null
            : _services?.Library.Playlists.FirstOrDefault(p => p.Id == SelectedPlaylist.Id);

    // ---- 리스트 CRUD (다이얼로그는 View가 띄우고 결과만 넘긴다) ----

    /// <summary>생성. 실패 사유는 안내로 표시. 성공 시 새 리스트를 선택한다.</summary>
    public async Task CreateAsync(string name)
    {
        if (_services is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowNotice(Loc.Get("Playlists_NameEmpty"), InfoBarSeverity.Error);
            return;
        }

        var created = _services.Library.Create(name);
        if (!created.IsSuccess || created.Value is null)
        {
            var message = created.Code == ErrorCode.LimitExceeded
                ? string.Format(Loc.Get("Playlists_ListLimit"), PlaylistLibrary.MaxPlaylists)
                : Loc.Get("Playlists_NameEmpty");
            ShowNotice(message, InfoBarSeverity.Error);
            return;
        }

        await _services.Library.SaveAsync();
        var entry = new PlaylistEntry(created.Value.Id, created.Value.Name, 0);
        Playlists.Add(entry);
        SelectedPlaylist = entry;
    }

    public async Task RenameAsync(PlaylistEntry entry, string newName)
    {
        if (_services is null)
        {
            return;
        }

        var renamed = _services.Library.Rename(entry.Id, newName);
        if (!renamed.IsSuccess)
        {
            ShowNotice(Loc.Get("Playlists_NameEmpty"), InfoBarSeverity.Error);
            return;
        }

        await _services.Library.SaveAsync();
        entry.Name = newName.Trim();
    }

    /// <summary>삭제 여부 사전 판단 — 재생 중인 리스트인지 (View가 확인 문구를 고르는 데 사용).</summary>
    public bool IsPlaying(PlaylistEntry entry) =>
        _services is not null &&
        _services.Coordinator.Status != PlaybackStatus.Stopped &&
        _services.Settings.LastPlaylistId == entry.Id;

    /// <summary>삭제 — 재생 중이면 정지 후 삭제 (plan T3 Edge, 확인은 View에서 완료된 상태).</summary>
    public async Task DeleteAsync(PlaylistEntry entry)
    {
        if (_services is null)
        {
            return;
        }

        if (IsPlaying(entry))
        {
            await _services.Coordinator.StopAsync();
        }

        var deleted = _services.Library.Delete(entry.Id);
        if (!deleted.IsSuccess)
        {
            return; // 이미 없음 — 목록 새로고침만
        }

        await _services.Library.SaveAsync();
        Playlists.Remove(entry);
        if (SelectedPlaylist == entry)
        {
            SelectedPlaylist = null;
        }
    }

    // ---- 항목 조작 ----

    [RelayCommand]
    private async Task AddItemAsync()
    {
        var playlist = FindSelected();
        if (_services is null || playlist is null)
        {
            return;
        }

        var parsed = YouTubeUrlParser.Parse(NewUrl);
        if (!parsed.IsSuccess || parsed.Value is null)
        {
            ShowNotice(Loc.Get("Playlists_InvalidUrl"), InfoBarSeverity.Error);
            return;
        }

        var added = _services.Library.AddItem(playlist.Id, NewUrl.Trim(), parsed.Value);
        if (!added.IsSuccess)
        {
            var message = added.Code == ErrorCode.LimitExceeded
                ? string.Format(Loc.Get("Playlists_ItemLimit"), PlaylistLibrary.MaxItemsPerPlaylist)
                : Loc.Get("Playlists_InvalidUrl");
            ShowNotice(message, InfoBarSeverity.Error);
            return;
        }

        NewUrl = string.Empty;
        IsNoticeOpen = false;
        await PersistItemsChangeAsync(playlist);
    }

    public async Task RemoveItemAsync(PlaylistItem item)
    {
        var playlist = FindSelected();
        if (_services is null || playlist is null)
        {
            return;
        }

        var removed = _services.Library.RemoveItem(playlist.Id, item.Id);
        if (removed.IsSuccess)
        {
            await PersistItemsChangeAsync(playlist);
        }
    }

    public async Task MoveItemAsync(PlaylistItem item, int delta)
    {
        var playlist = FindSelected();
        if (_services is null || playlist is null)
        {
            return;
        }

        var from = playlist.Items.FindIndex(i => i.Id == item.Id);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= playlist.Items.Count)
        {
            return;
        }

        var moved = _services.Library.MoveItem(playlist.Id, from, to);
        if (moved.IsSuccess)
        {
            await PersistItemsChangeAsync(playlist);
        }
    }

    /// <summary>
    /// 드래그 정렬 완료 후 뷰 컬렉션 순서를 모델에 반영한다 (View의 CollectionChanged에서 호출).
    /// 드래그 중간 상태(개수 불일치)는 무시한다.
    /// </summary>
    public async Task SyncOrderFromViewAsync()
    {
        var playlist = FindSelected();
        if (_services is null || playlist is null || _syncingItems || Items.Count != playlist.Items.Count)
        {
            return;
        }

        var viewOrder = Items.Select(i => i.Id).ToList();
        var modelOrder = playlist.Items.Select(i => i.Id).ToList();
        if (viewOrder.SequenceEqual(modelOrder))
        {
            return;
        }

        var byId = playlist.Items.ToDictionary(i => i.Id);
        playlist.Items.Clear();
        playlist.Items.AddRange(viewOrder.Select(id => byId[id]));

        await _services.Library.SaveAsync();
        await _services.Coordinator.NotifyPlaylistChangedAsync(playlist.Id);
        SelectedPlaylist!.ItemCount = playlist.Items.Count;
        CanAddItem = playlist.Items.Count < PlaylistLibrary.MaxItemsPerPlaylist;
    }

    /// <summary>항목 변경 공통 마무리 — 저장, 재생 큐 반영, 뷰 갱신.</summary>
    private async Task PersistItemsChangeAsync(Playlist playlist)
    {
        await _services!.Library.SaveAsync();
        await _services.Coordinator.NotifyPlaylistChangedAsync(playlist.Id);
        RefreshItems();
        if (SelectedPlaylist is not null)
        {
            SelectedPlaylist.ItemCount = playlist.Items.Count;
        }
    }

    // ---- 재생 ----

    [RelayCommand]
    private async Task PlayAsync()
    {
        var playlist = FindSelected();
        if (_services is null || playlist is null)
        {
            return;
        }

        var result = await _services.Coordinator.StartAsync(playlist.Id);
        if (!result.IsSuccess)
        {
            AppLog.Write($"플레이리스트 재생 시작 실패: {result.Message}");
            var message = result.Code == ErrorCode.InvalidInput
                ? Loc.Get("Playlists_EmptyList")
                : Loc.Get("Playlists_PlayFailed");
            ShowNotice(message, InfoBarSeverity.Error);
        }
    }

    private void ShowNotice(string message, InfoBarSeverity severity)
    {
        NoticeMessage = message;
        NoticeSeverity = severity;
        IsNoticeOpen = true;
    }
}
