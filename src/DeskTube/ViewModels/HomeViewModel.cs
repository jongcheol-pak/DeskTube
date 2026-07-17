using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskTube.Services;
using Microsoft.UI.Xaml.Controls;

namespace DeskTube.ViewModels;

/// <summary>홈 빠른 재생 칩 1개 (플레이리스트 요약 — restyle plan T5, 시안 chips).
/// 재생 중 표시가 실시간 갱신돼야 해 record 대신 관찰 가능 클래스 (now-playing plan T3·D5, PlaylistEntry 선례).</summary>
public sealed partial class QuickChip : ObservableObject
{
    public QuickChip(Guid id, string name, string countText)
    {
        Id = id;
        Name = name;
        CountText = countText;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string CountText { get; }

    /// <summary>지금 배경 재생 중인 리스트 — 스피커 글리프 표시용.</summary>
    [ObservableProperty]
    public partial bool IsNowPlaying { get; set; }
}

/// <summary>
/// 홈 — URL 입력·즉시 재생 + 재생 중 pill + 모니터 카드 + 빠른 재생 칩 (restyle plan T5, 시안).
/// 즉시 재생은 "빠른 재생" 플레이리스트를 만들어 그 항목을 교체하는 방식 —
/// part1 Coordinator가 플레이리스트 단위로만 재생하므로 공개 계약을 바꾸지 않는다.
/// 리스트 식별은 안정 ID(Settings.QuickPlaylistId — 표시 이름은 언어 전환·동명 리스트와 충돌).
/// </summary>
public partial class HomeViewModel : ObservableObject
{
    private AppServices? _services;
    private Microsoft.UI.Dispatching.DispatcherQueue? _dispatcher;

    /// <summary>마지막 URL 복원을 1회만 수행 — 사용자가 지운 입력을 재진입 때 되살리지 않는다 (FR-1).</summary>
    private bool _urlRestored;

    public HomeViewModel()
    {
        MonitorPanel.MonitorsRefreshed += OnMonitorStateChanged;
        MonitorPanel.SelectionChanged += OnMonitorStateChanged;
        MonitorPanel.NoticeRequested += OnPanelNoticeRequested;

        // partial property는 초기화 식을 못 가짐 (MVVMTK0045 대응으로 필드 대신 채택)
        Url = string.Empty;
        PlayingLabel = string.Empty;
    }

    /// <summary>모니터 카드 패널 (설정과 공유하는 공용 VM — 선택 상태의 정본은 AppSettings).</summary>
    public MonitorPanelViewModel MonitorPanel { get; } = new();

    /// <summary>유튜브 계정 상태 패널 (FR-15) — 설정과 같은 전역 공유 인스턴스 (상태 정본 단일화).</summary>
    public AccountPanelViewModel Account => AccountPanelViewModel.Shared;

    /// <summary>빠른 재생 칩 (플레이리스트 요약 — 클릭 시 View가 플레이리스트 페이지로 이동, D5).</summary>
    public ObservableCollection<QuickChip> QuickChips { get; } = [];

    [ObservableProperty]
    public partial string Url { get; set; }

    /// <summary>재생 중 pill 표시 (Stopped 외 상태 — 일시정지 중에도 정지 가능해야 함).</summary>
    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    /// <summary>"디스플레이 1·2에서 재생 중" (시안 playingLabel — 선택 모니터 번호 나열).</summary>
    [ObservableProperty]
    public partial string PlayingLabel { get; set; }

    [ObservableProperty]
    public partial bool HasChips { get; set; }

    /// <summary>
    /// 페이지 진입 시 호출 (SettingsViewModel과 동일한 지연 초기화 패턴).
    /// 캐시 재진입에도 호출돼 모니터·칩·재생 상태를 재동기화한다 (plan T5 Edge — stale 방지).
    /// </summary>
    public void Load()
    {
        if (App.Services is null)
        {
            App.ServicesInitialized += OnServicesInitialized;
            return;
        }

        AttachCore(App.Services);
    }

    /// <summary>페이지 이탈 시 호출 — 구독 해제 (Loaded/Unloaded 대칭, 누수 방지).</summary>
    public void Detach()
    {
        App.ServicesInitialized -= OnServicesInitialized;
        if (_services is not null)
        {
            _services.Coordinator.StatusChanged -= OnStatusChanged;
        }

        MonitorPanel.Detach();
    }

    private void OnServicesInitialized(object? sender, EventArgs e)
    {
        App.ServicesInitialized -= OnServicesInitialized;
        AttachCore(App.Services!);
    }

    private void AttachCore(AppServices services)
    {
        if (_services is null)
        {
            _services = services;
            _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        }

        // 재진입마다 해제 후 재구독 (Detach와 대칭 — MonitorPanel.Attach와 동일 멱등 패턴)
        services.Coordinator.StatusChanged -= OnStatusChanged;
        services.Coordinator.StatusChanged += OnStatusChanged;

        // 마지막 재생 URL 복원 표시 (FR-1) — 최초 1회만, 입력 중인 값은 덮지 않는다
        if (!_urlRestored && Url.Length == 0 && services.Settings.LastHomeUrl is { Length: > 0 } lastUrl)
        {
            Url = lastUrl;
        }
        _urlRestored = true;

        MonitorPanel.Attach(services);
        Account.Attach(services); // 공유 인스턴스 연결 — 프로브는 최초 1회, 이후 상태 변화는 로그인 창 닫힘/로그아웃이 갱신
        RefreshChips();
        UpdatePlaybackState(services.Coordinator.Status);
    }

    /// <summary>정지 pill 버튼 (시안 "■ 정지") — 상태 갱신은 StatusChanged 이벤트가 반영.</summary>
    [RelayCommand]
    private async Task StopAsync()
    {
        if (_services is null)
        {
            return;
        }

        await _services.Coordinator.StopAsync();
    }

    /// <summary>StatusChanged는 발생 스레드가 보장되지 않아 UI로 마셜링 (plan T5 Edge).</summary>
    private void OnStatusChanged(object? sender, PlaybackStatus status) =>
        _dispatcher?.TryEnqueue(() => UpdatePlaybackState(status));

    private void UpdatePlaybackState(PlaybackStatus status)
    {
        IsPlaying = status != PlaybackStatus.Stopped;
        UpdatePlayingLabel();
        UpdateChipNowPlaying();
    }

    /// <summary>칩 재생 중 글리프 갱신 — 정본은 Coordinator.CurrentPlaylistId (정지 시 null → 전부 해제).</summary>
    private void UpdateChipNowPlaying()
    {
        var currentId = _services?.Coordinator.CurrentPlaylistId;
        foreach (var chip in QuickChips)
        {
            chip.IsNowPlaying = chip.Id == currentId;
        }
    }

    private void OnMonitorStateChanged(object? sender, EventArgs e) => UpdatePlayingLabel();

    private void UpdatePlayingLabel()
    {
        var numbers = string.Join("·", MonitorPanel.Monitors.Where(m => m.IsSelected).Select(m => m.Number));
        PlayingLabel = string.Format(Loc.Get("Home_PlayingFormat"), numbers);
    }

    /// <summary>빠른 재생 칩 재구성 — 페이지 진입마다 리스트 이름·곡수 최신화.</summary>
    private void RefreshChips()
    {
        if (_services is null)
        {
            return; // PlayAsync가 Attach 전에 실행된 드문 경로 — 다음 진입 때 채워짐
        }

        QuickChips.Clear();
        var currentId = _services.Coordinator.CurrentPlaylistId; // 재구성 시점 초기 반영 (이후 갱신은 StatusChanged)
        foreach (var playlist in _services.Library.Playlists)
        {
            QuickChips.Add(new QuickChip(
                playlist.Id,
                playlist.Name,
                string.Format(Loc.Get("Home_ChipCountFormat"), playlist.Items.Count))
            {
                IsNowPlaying = playlist.Id == currentId,
            });
        }

        HasChips = QuickChips.Count > 0;
    }

    private void OnPanelNoticeRequested(object? sender, string message) =>
        ToastService.Show(message, InfoBarSeverity.Warning);

    [RelayCommand]
    private async Task PlayAsync()
    {
        var services = App.Services;
        if (services is null)
        {
            ToastService.Show(Loc.Get("Common_NotReady"), InfoBarSeverity.Warning);
            return;
        }

        var parsed = YouTubeUrlParser.Parse(Url);
        if (!parsed.IsSuccess || parsed.Value is null)
        {
            // 입력은 유지 — 사용자가 고쳐서 재시도 (plan T2 Edge)
            ToastService.Show(Loc.Get("Home_InvalidUrl"), InfoBarSeverity.Error);
            return;
        }

        // "빠른 재생" 리스트는 안정 ID(Settings.QuickPlaylistId)로 식별한다 (2026-07-17 수정 —
        // 이름 식별은 언어 전환·동명 사용자 리스트와 충돌). ID 미기록(구버전 데이터)이면 이름 폴백 1회.
        var playlist = services.Settings.QuickPlaylistId is { } quickId
            ? services.Library.Playlists.FirstOrDefault(p => p.Id == quickId)
            : services.Library.Playlists.FirstOrDefault(p => p.Name == Loc.Get("Home_QuickPlaylistName"));
        if (playlist is null)
        {
            var created = services.Library.Create(Loc.Get("Home_QuickPlaylistName"));
            if (!created.IsSuccess || created.Value is null)
            {
                AppLog.Write($"빠른 재생 리스트 생성 실패: {created.Message}");
                ToastService.Show(Loc.Get("Home_PlayFailed"), InfoBarSeverity.Error);
                return;
            }

            playlist = created.Value;
        }

        // 항목을 이번 URL 1개로 교체
        playlist.Items.Clear();
        var added = services.Library.AddItem(playlist.Id, Url.Trim(), parsed.Value);
        if (!added.IsSuccess)
        {
            AppLog.Write($"빠른 재생 항목 추가 실패: {added.Message}");
            ToastService.Show(Loc.Get("Home_PlayFailed"), InfoBarSeverity.Error);
            return;
        }

        await services.Library.SaveAsync();

        // 마지막 URL·빠른 재생 ID 영속은 StartAsync 성공 경로의 자체 저장에 편승 (이중 settings.json 저장 제거).
        // StartAsync는 실패 경로에서 저장하지 않으므로 실패 시 URL만 원복하면 "성공 시에만 기록"(FR-1)이 유지된다.
        var previousUrl = services.Settings.LastHomeUrl;
        services.Settings.LastHomeUrl = Url.Trim();
        services.Settings.QuickPlaylistId = playlist.Id;

        var startResult = await services.Coordinator.StartAsync(playlist.Id);
        if (!startResult.IsSuccess)
        {
            services.Settings.LastHomeUrl = previousUrl; // 실패한 URL이 이후 다른 저장에 편승해 남지 않게 원복
            AppLog.Write($"즉시 재생 시작 실패: {startResult.Message}");
            ToastService.Show(Loc.Get("Home_PlayFailed"), InfoBarSeverity.Error);
            return;
        }

        RefreshChips(); // "빠른 재생" 리스트 생성·곡 교체가 칩 표시에 반영되게
        ToastService.Show(Loc.Get("Home_PlayStarted"), InfoBarSeverity.Success);
    }
}
