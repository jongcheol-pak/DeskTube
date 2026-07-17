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

    /// <summary>현재 선택된 리스트 — 시안 활성 표시(코럴 인디케이터·텍스트 강조)용 (restyle T6).</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }
}

/// <summary>
/// 우측 목록의 항목 1건 — 순위·메타데이터 표시 상태 (FR-18, plan D9).
/// 비동기 메타 도착 시 행이 갱신되도록 표시 필드만 관찰 가능으로 둔다.
/// </summary>
public partial class PlaylistItemEntry : ObservableObject
{
    public PlaylistItemEntry(PlaylistItem item, int rank)
    {
        Id = item.Id;
        Url = item.Url;
        VideoId = item.VideoId;
        Rank = rank;
        Title = item.Title;
        ChannelName = item.ChannelName;
    }

    public Guid Id { get; }

    public string Url { get; }

    public string VideoId { get; }

    /// <summary>썸네일 URI — 임베드 플레이어와 같은 유튜브 CDN, 영상 ID 외 정보 미전송 (FR-18).
    /// Uri 타입인 이유: x:Bind는 string→Uri 변환이 없어 BitmapImage.UriSource에 직결하려면 Uri여야 한다.</summary>
    public Uri ThumbnailUri => new($"https://i.ytimg.com/vi/{VideoId}/mqdefault.jpg");

    [ObservableProperty]
    public partial int Rank { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string ChannelName { get; set; }

    /// <summary>표시 제목 — 메타 미조회·조회 실패 시 URL 폴백 (plan D1).</summary>
    public string DisplayTitle => string.IsNullOrEmpty(Title) ? Url : Title;
}

/// <summary>
/// 플레이리스트 관리 (PRD FR-6 UI, plan T3 + FR-18 개편).
/// 좌측 리스트 CRUD + 우측 항목 추가/삭제/이동 + 재생(전체/셔플/행 단위). 변경 즉시 저장하고,
/// 재생 중인 리스트 변경은 Coordinator.NotifyPlaylistChangedAsync로 큐에 반영한다.
/// 항목 메타데이터(제목·채널)는 oEmbed로 지연 채움(backfill)하고 실패는 URL 표시로 폴백한다.
/// </summary>
public partial class PlaylistsViewModel : ObservableObject
{
    /// <summary>메타데이터 backfill 동시 요청 상한 — 1000개 리스트에서도 폭주 방지 (plan D10).</summary>
    private const int MetadataConcurrency = 4;

    private AppServices? _services;

    /// <summary>드래그 정렬 동기화 중 재진입 억제 (뷰 컬렉션 재구성 시).</summary>
    private bool _syncingItems;

    /// <summary>진행 중 backfill 취소 — 리스트 전환·페이지 이탈 시 늦은 응답이 반영되지 않게 (plan D10).</summary>
    private CancellationTokenSource? _metadataCts;

    public PlaylistsViewModel()
    {
        NewUrl = string.Empty;
    }

    public ObservableCollection<PlaylistEntry> Playlists { get; } = [];

    /// <summary>선택된 리스트의 항목 (드래그 정렬을 위해 뷰 전용 컬렉션 — 모델과 순서 동기화).</summary>
    public ObservableCollection<PlaylistItemEntry> Items { get; } = [];

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

    public void Detach()
    {
        App.ServicesInitialized -= OnServicesInitialized;
        CancelMetadataBackfill(); // 페이지 이탈 — 늦은 응답 반영 방지 (plan D10)
    }

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

        // 칩 진입 등으로 예약된 선택 적용 — 목록 재구성이 선택을 비우므로 여기서 소비 (restyle T5)
        ApplyPendingSelection();
        _pendingSelectionId = null;

        // 기본 선택 — 칩 예약 > 마지막 선택 > 첫 리스트 (FR-18 보강, 배지 후속 plan T2·D3)
        if (SelectedPlaylist is null && Playlists.Count > 0)
        {
            var lastId = services.Settings.LastSelectedPlaylistId;
            SelectedPlaylist = (lastId is Guid id ? Playlists.FirstOrDefault(p => p.Id == id) : null)
                ?? Playlists[0];
        }
    }

    /// <summary>홈 빠른 재생 칩 진입 시 예약할 선택 (Populate가 소비 — restyle T5·D5).</summary>
    private Guid? _pendingSelectionId;

    /// <summary>
    /// 지정 리스트 선택 (홈 칩 → 페이지 이동 진입점, restyle T5·D5).
    /// 페이지 Loaded의 Populate가 목록을 재구성하며 선택을 비우므로, 예약해 두고 즉시도 시도한다.
    /// 리스트가 그 사이 삭제됐으면 조용히 무시 (plan T5 Edge — 선택 없음 폴백).
    /// </summary>
    public void SelectPlaylist(Guid playlistId)
    {
        _pendingSelectionId = playlistId;
        ApplyPendingSelection();
    }

    private void ApplyPendingSelection()
    {
        if (_pendingSelectionId is not Guid id)
        {
            return;
        }

        var entry = Playlists.FirstOrDefault(p => p.Id == id);
        if (entry is not null)
        {
            SelectedPlaylist = entry;
        }
    }

    partial void OnSelectedPlaylistChanged(PlaylistEntry? value)
    {
        // 시안 활성 표시 — 선택 항목만 코럴 인디케이터·강조 텍스트 (restyle T6)
        foreach (var entry in Playlists)
        {
            entry.IsActive = ReferenceEquals(entry, value);
        }

        // 마지막 선택 기억 — null(목록 재구성·삭제 중 일시 해제)은 기록하지 않는다 (plan T2·D3)
        if (value is not null && _services is not null
            && _services.Settings.LastSelectedPlaylistId != value.Id)
        {
            _services.Settings.LastSelectedPlaylistId = value.Id;
            _ = SaveLastSelectionAsync();
        }

        RefreshItems();
    }

    /// <summary>마지막 선택 영속화 — 실패해도 화면 동작에 영향 없음 (MonitorPanelViewModel.ApplyAsync 선례).</summary>
    private async Task SaveLastSelectionAsync()
    {
        try
        {
            await _services!.Store.SaveSettingsAsync(_services.Settings);
        }
        catch (Exception ex)
        {
            AppLog.Write($"마지막 선택 저장 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }

    /// <summary>우측 항목 컬렉션을 모델에서 다시 채운다 (선택 변경·항목 조작 후). 메타 누락분은 백그라운드 채움.</summary>
    private void RefreshItems()
    {
        CancelMetadataBackfill(); // 리스트 전환 — 이전 리스트의 늦은 응답 차단 (plan D10)

        Playlist? playlist;
        _syncingItems = true;
        try
        {
            Items.Clear();
            playlist = FindSelected();
            if (playlist is not null)
            {
                for (var i = 0; i < playlist.Items.Count; i++)
                {
                    Items.Add(new PlaylistItemEntry(playlist.Items[i], rank: i + 1));
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

        if (playlist is not null)
        {
            StartMetadataBackfill(playlist);
        }
    }

    // ---- 메타데이터 채움 (FR-18, plan D10) ----

    private void CancelMetadataBackfill()
    {
        // Dispose는 하지 않는다 — 진행 중 backfill이 disposed 토큰을 만지는 경합 방지
        // (타이머 없는 CTS는 GC 수거로 충분)
        _metadataCts?.Cancel();
        _metadataCts = null;
    }

    /// <summary>제목 없는 항목만 oEmbed로 채운다 — 신규 추가 직후·기존 데이터 소급 공용 경로.</summary>
    private void StartMetadataBackfill(Playlist playlist)
    {
        if (_services is null)
        {
            return;
        }

        var missing = Items.Where(e => e.Title.Length == 0).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        _metadataCts = new CancellationTokenSource();
        var token = _metadataCts.Token;
        _ = RunMetadataBackfillAsync(playlist, missing, token); // fire-and-forget — 내부에서 예외 흡수
    }

    /// <summary>
    /// backfill 본체 — 동시 4개 제한, 전부 끝난 뒤 성공분이 있으면 1회만 저장.
    /// UI 스레드에서 시작해 await 후속도 UI 컨텍스트로 돌아오므로 Entry 갱신은 스레드 안전.
    /// </summary>
    private async Task RunMetadataBackfillAsync(Playlist playlist, List<PlaylistItemEntry> entries, CancellationToken token)
    {
        try
        {
            using var limiter = new SemaphoreSlim(MetadataConcurrency);
            var anySuccess = false;

            async Task FillOneAsync(PlaylistItemEntry entry)
            {
                await limiter.WaitAsync(token);
                try
                {
                    var fetched = await _services!.Metadata.FetchAsync(entry.VideoId, token);
                    if (!fetched.IsSuccess || fetched.Value is null)
                    {
                        return; // 실패는 조용히 URL 폴백 유지 — 다음 진입 시 자연 재시도 (plan D10)
                    }

                    // 모델(영속 대상)과 Entry(표시)를 함께 갱신 — 삭제 경합 시 모델에 없으면 표시만 갱신돼도 무해
                    var model = playlist.Items.FirstOrDefault(i => i.Id == entry.Id);
                    if (model is not null)
                    {
                        model.Title = fetched.Value.Title;
                        model.ChannelName = fetched.Value.ChannelName;
                        anySuccess = true;
                    }

                    entry.Title = fetched.Value.Title;
                    entry.ChannelName = fetched.Value.ChannelName;
                }
                finally
                {
                    limiter.Release();
                }
            }

            await Task.WhenAll(entries.Select(FillOneAsync));

            if (anySuccess && !token.IsCancellationRequested)
            {
                await _services!.Library.SaveAsync(); // 항목당 저장 대신 1회 저장 (plan D10)
            }
        }
        catch (OperationCanceledException)
        {
            // 리스트 전환·페이지 이탈에 의한 정상 취소
        }
        catch (Exception ex)
        {
            AppLog.Write($"메타데이터 채움 중 오류: {ex.GetType().Name} {ex.Message}");
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

    public async Task RemoveItemAsync(PlaylistItemEntry item)
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

    public async Task MoveItemAsync(PlaylistItemEntry item, int delta)
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

        // 드래그로 순서가 바뀌었으므로 순위 표시 재계산 (plan T4 Edge)
        for (var i = 0; i < Items.Count; i++)
        {
            Items[i].Rank = i + 1;
        }

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

    // ---- 재생 (FR-18 — 전체듣기/셔플듣기/행 재생 3진입점이 공통 헬퍼 공유, plan 4-D) ----

    /// <summary>전체듣기 — 목록 순서대로 리스트 처음부터 (끝나면 반복 — FR-7).</summary>
    [RelayCommand]
    private Task PlayAsync() => StartPlaybackAsync(startItemId: null, shuffle: false);

    /// <summary>셔플듣기 — 재생 모드를 셔플로 바꿔(설정에 영속, plan D4) 시작.</summary>
    [RelayCommand]
    private Task ShuffleAllAsync() => StartPlaybackAsync(startItemId: null, shuffle: true);

    /// <summary>행 재생 — 해당 항목부터 목록 순서대로 (plan D3, View의 행 버튼 핸들러가 호출).</summary>
    public Task PlayItemAsync(PlaylistItemEntry entry) => StartPlaybackAsync(entry.Id, shuffle: false);

    private async Task StartPlaybackAsync(Guid? startItemId, bool shuffle)
    {
        var playlist = FindSelected();
        if (_services is null || playlist is null)
        {
            return;
        }

        // 진입점이 모드를 항상 명시한다 — 설정에 재생 순서 항목이 없어(2026-07-17 제거)
        // 마지막 모드를 따라가면 셔플듣기 후 전체듣기도 셔플로 고착되기 때문 (plan D2).
        await _services.Coordinator.SetModeAsync(shuffle ? PlaybackMode.Shuffle : PlaybackMode.Sequential);

        var result = await _services.Coordinator.StartAsync(playlist.Id, startItemId);
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
