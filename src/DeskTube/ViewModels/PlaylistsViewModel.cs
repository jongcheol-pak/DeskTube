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

    /// <summary>지금 배경 재생 중인 리스트 — 스피커 글리프 표시용 (선택 IsActive와 별개 상태).</summary>
    [ObservableProperty]
    public partial bool IsNowPlaying { get; set; }
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
        DurationSeconds = item.DurationSeconds;
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

    /// <summary>지금 배경 재생 중인 곡 — 순위 자리 스피커 글리프 표시용 (PlaylistEntry.IsNowPlaying 항목판, now-playing item plan T2).</summary>
    [ObservableProperty]
    public partial bool IsNowPlaying { get; set; }

    /// <summary>재생시간(초, FR-18) — 재생 시 수집돼 도착하면 행 표시가 갱신되도록 관찰 가능. 0 = 미수집.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationText))]
    public partial int DurationSeconds { get; set; }

    /// <summary>표시용 재생시간 — 미수집(0 이하)이면 공란 (plan D3·D10).</summary>
    public string DurationText => FormatDuration(DurationSeconds);

    /// <summary>초를 "m:ss"(1시간 미만) 또는 "h:mm:ss"(1시간 이상)로 포맷한다. 0 이하는 빈 문자열 (미수집 공란).
    /// 숫자·콜론만이라 지역화 불요 (plan D10·D11). 순수 함수 — 단위 테스트 대상.</summary>
    public static string FormatDuration(int seconds)
    {
        if (seconds <= 0)
        {
            return string.Empty;
        }

        var time = TimeSpan.FromSeconds(seconds);
        return seconds >= 3600
            ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes}:{time.Seconds:D2}";
    }
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
    private Microsoft.UI.Dispatching.DispatcherQueue? _dispatcher;

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

    /// <summary>선택한 리스트가 셔플 모드로 재생(일시정지 포함) 중인가 — 셔플듣기 버튼의 트레일링 정지 아이콘 표시.
    /// 정본은 Coordinator.CurrentPlaylistId + Settings.Mode이고 이 값은 미러다 (mode-indicator plan D1·D2).</summary>
    [ObservableProperty]
    public partial bool IsShufflePlaying { get; set; }

    /// <summary>선택한 리스트가 비셔플 모드(순차·내부 잔존 모드 포함)로 재생 중인가 — 전체듣기 버튼의
    /// 트레일링 정지 아이콘 표시 (IsShufflePlaying과 상호배타, mode-indicator plan D3).</summary>
    [ObservableProperty]
    public partial bool IsSequentialPlaying { get; set; }

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
        if (_services is not null)
        {
            _services.Coordinator.StatusChanged -= OnStatusChanged;
            _services.Coordinator.CurrentItemChanged -= OnCurrentItemChanged;
            _services.Coordinator.ItemDurationCaptured -= OnItemDurationCaptured;
        }

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
        _dispatcher ??= Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        // 재진입마다 해제 후 재구독 (Detach와 대칭 — HomeViewModel과 동일 멱등 패턴)
        services.Coordinator.StatusChanged -= OnStatusChanged;
        services.Coordinator.StatusChanged += OnStatusChanged;
        services.Coordinator.CurrentItemChanged -= OnCurrentItemChanged;
        services.Coordinator.CurrentItemChanged += OnCurrentItemChanged;
        services.Coordinator.ItemDurationCaptured -= OnItemDurationCaptured;
        services.Coordinator.ItemDurationCaptured += OnItemDurationCaptured;

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

        UpdateNowPlaying(); // 재생 중 페이지 진입 — 리스트 초기 상태 반영 (now-playing plan T2)
        UpdateNowPlayingItem(); // 선택 불변 재진입(RefreshItems 미발생) 시에도 항목 표시 반영 (now-playing item plan T2)
    }

    /// <summary>StatusChanged는 발생 스레드가 보장되지 않아 UI로 마셜링 (HomeViewModel과 동일 패턴).</summary>
    private void OnStatusChanged(object? sender, PlaybackStatus status) =>
        _dispatcher?.TryEnqueue(UpdateNowPlaying);

    /// <summary>CurrentItemChanged도 발생 스레드가 보장되지 않아 UI로 마셜링 (OnStatusChanged와 동일 패턴).
    /// 같은 리스트 내 곡 전환은 StatusChanged가 미발화하므로 항목 표시 갱신은 이 이벤트에 의존한다.</summary>
    private void OnCurrentItemChanged(object? sender, EventArgs e) =>
        _dispatcher?.TryEnqueue(UpdateNowPlayingItem);

    /// <summary>재생시간 수집 알림 — 발생 스레드 비보장이라 UI로 마셜링. 인자 ID로 대상 항목을 특정한다
    /// (CurrentItemId 읽기 대신 — 마셜링 지연 중 곡 전환 시 오갱신 방지, Coordinator D9와 대응).</summary>
    private void OnItemDurationCaptured(object? sender, Guid itemId) =>
        _dispatcher?.TryEnqueue(() => ApplyCapturedDuration(itemId));

    /// <summary>수집된 재생시간을 현재 목록의 해당 항목에 반영 — 값 정본은 라이브러리 모델.
    /// 다른 리스트를 보는 중이면 항목이 없어 no-op (다음 진입 시 생성자 복사로 반영).</summary>
    private void ApplyCapturedDuration(Guid itemId)
    {
        var entry = Items.FirstOrDefault(e => e.Id == itemId);
        var model = FindSelected()?.Items.FirstOrDefault(i => i.Id == itemId);
        if (entry is not null && model is not null)
        {
            entry.DurationSeconds = model.DurationSeconds;
        }
    }

    /// <summary>재생 중 리스트 글리프 갱신 — 정본은 Coordinator.CurrentPlaylistId (정지 시 null → 전부 해제).
    /// 상단 듣기 버튼의 모드별 정지 아이콘 상태도 같은 정본에서 함께 갱신한다 (mode-indicator plan T1).</summary>
    private void UpdateNowPlaying()
    {
        var currentId = _services?.Coordinator.CurrentPlaylistId;
        foreach (var entry in Playlists)
        {
            entry.IsNowPlaying = entry.Id == currentId;
        }

        UpdateModePlaying();
    }

    /// <summary>모드별 재생 상태 갱신 — "선택 리스트가 재생 중인가"를 정본(Coordinator.CurrentPlaylistId)에서
    /// 직접 판정한다 (호출부마다 술어를 계산하면 표현이 갈라져 드리프트 위험 — 2026-07-18 정리).
    /// 모드 정본은 Coordinator.Settings.Mode (SetModeAsync가 설정·실행 큐를 함께 갱신하므로 재생 중 불변 —
    /// plan D2). 셔플 외 모드는 UI 진입점이 전체듣기·행 재생뿐이라 비셔플 묶음으로 전체듣기 쪽에 표시한다 (plan D3).</summary>
    private void UpdateModePlaying()
    {
        // null 가드 필수 — 정지 상태(CurrentPlaylistId == null)에서 선택도 null이면 == 비교가 true가 된다
        var playing = SelectedPlaylist is not null
            && _services?.Coordinator.CurrentPlaylistId == SelectedPlaylist.Id;
        var shuffle = _services?.Coordinator.Settings.Mode == PlaybackMode.Shuffle;
        IsShufflePlaying = playing && shuffle;
        IsSequentialPlaying = playing && !shuffle;
    }

    /// <summary>재생 중 항목 글리프 갱신 — 정본은 Coordinator.CurrentItemId (정지 시 null → 전부 해제).
    /// 우측 목록의 재생 중인 곡만 순위 자리에 스피커 글리프를 표시한다 (UpdateNowPlaying 항목판).</summary>
    private void UpdateNowPlayingItem()
    {
        var currentId = _services?.Coordinator.CurrentItemId;
        foreach (var entry in Items)
        {
            entry.IsNowPlaying = entry.Id == currentId;
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

        // 선택이 바뀌면 모드별 정지 아이콘 상태도 즉시 재판정 — StatusChanged 없이 선택만 바뀌는 경우 대비
        // (mode-indicator plan T1). SelectedPlaylist는 이 시점에 이미 value로 대입돼 있다 (MVVM Toolkit 규약).
        UpdateModePlaying();

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
                ToastService.Show(
                    string.Format(Loc.Get("Playlists_ItemLimit"), PlaylistLibrary.MaxItemsPerPlaylist),
                    InfoBarSeverity.Warning);
            }
        }
        finally
        {
            _syncingItems = false;
        }

        UpdateNowPlayingItem(); // 재구성된 Items에 재생 중 항목 반영 (선택 변경·페이지 진입 — now-playing item plan D4)

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
            ToastService.Show(Loc.Get("Playlists_NameEmpty"), InfoBarSeverity.Error);
            return;
        }

        var created = _services.Library.Create(name);
        if (!created.IsSuccess || created.Value is null)
        {
            var message = created.Code == ErrorCode.LimitExceeded
                ? string.Format(Loc.Get("Playlists_ListLimit"), PlaylistLibrary.MaxPlaylists)
                : Loc.Get("Playlists_NameEmpty");
            ToastService.Show(message, InfoBarSeverity.Error);
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
            ToastService.Show(Loc.Get("Playlists_NameEmpty"), InfoBarSeverity.Error);
            return;
        }

        await _services.Library.SaveAsync();
        entry.Name = newName.Trim();
    }

    /// <summary>삭제 여부 사전 판단 — 재생 중인 리스트인지 (View가 확인 문구를 고르는 데 사용).</summary>
    public bool IsPlaying(PlaylistEntry entry) =>
        _services is not null && _services.Coordinator.CurrentPlaylistId == entry.Id;

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
            ToastService.Show(Loc.Get("Playlists_InvalidUrl"), InfoBarSeverity.Error);
            return;
        }

        var added = _services.Library.AddItem(playlist.Id, NewUrl.Trim(), parsed.Value);
        if (!added.IsSuccess)
        {
            var message = added.Code == ErrorCode.LimitExceeded
                ? string.Format(Loc.Get("Playlists_ItemLimit"), PlaylistLibrary.MaxItemsPerPlaylist)
                : Loc.Get("Playlists_InvalidUrl");
            ToastService.Show(message, InfoBarSeverity.Error);
            return;
        }

        NewUrl = string.Empty;
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

    /// <summary>전체듣기 토글 — 선택 리스트가 비셔플 모드로 재생(일시정지 포함) 중이면 정지,
    /// 아니면 목록 순서대로 리스트 처음부터 (끝나면 반복 — FR-7, 모드별 토글은 mode-indicator plan D4).</summary>
    [RelayCommand]
    private async Task PlayAsync()
    {
        if (await TryStopIfPlayingAsync(IsSequentialPlaying))
        {
            return;
        }

        await StartPlaybackAsync(startItemId: null, shuffle: false);
    }

    /// <summary>셔플듣기 토글 — 선택 리스트가 셔플 모드로 재생(일시정지 포함) 중이면 정지,
    /// 아니면 재생 모드를 셔플로 바꿔(설정에 영속, 멜론 plan D4) 시작 (mode-indicator plan D4).</summary>
    [RelayCommand]
    private async Task ShuffleAllAsync()
    {
        if (await TryStopIfPlayingAsync(IsShufflePlaying))
        {
            return;
        }

        await StartPlaybackAsync(startItemId: null, shuffle: true);
    }

    /// <summary>행 재생/정지 토글 — 재생 중인 곡의 행이면 정지, 아니면 해당 항목부터 목록 순서대로
    /// (FR-18 행 재생 + stop-toggle plan D2, View의 행 버튼 핸들러가 호출).</summary>
    public async Task TogglePlayItemAsync(PlaylistItemEntry entry)
    {
        if (await TryStopIfPlayingAsync(entry.IsNowPlaying))
        {
            return;
        }

        await StartPlaybackAsync(entry.Id, shuffle: false);
    }

    /// <summary>재생/정지 토글 공통 정지 경로 — 진입점이 "재생 중"이면 정지하고 true (stop-toggle plan D2).</summary>
    private async Task<bool> TryStopIfPlayingAsync(bool isPlaying)
    {
        if (!isPlaying || _services is null)
        {
            return false;
        }

        await _services.Coordinator.StopAsync();
        return true;
    }

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
            ToastService.Show(message, InfoBarSeverity.Error);
        }
    }

    /// <summary>공유 팝업의 링크 복사 성공 알림 (페이지 코드비하인드 진입점 — share plan T1).</summary>
    public void NotifyLinkCopied() => ToastService.Show(Loc.Get("Playlists_LinkCopied"), InfoBarSeverity.Success);

    /// <summary>공유 팝업의 링크 복사 실패 알림 (클립보드 접근 실패 등 드문 경우).</summary>
    public void NotifyLinkCopyFailed() => ToastService.Show(Loc.Get("Playlists_ShareCopyFailed"), InfoBarSeverity.Error);

}
