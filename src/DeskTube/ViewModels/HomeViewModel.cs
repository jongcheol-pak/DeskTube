using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskTube.Services;
using Microsoft.UI.Xaml.Controls;

namespace DeskTube.ViewModels;

/// <summary>홈 빠른 재생 칩 1개 (플레이리스트 요약 — restyle plan T5, 시안 chips).</summary>
public sealed record QuickChip(Guid Id, string Name, string CountText);

/// <summary>
/// 홈 — URL 입력·즉시 재생 + 재생 중 pill + 모니터 카드 + 빠른 재생 칩 (restyle plan T5, 시안).
/// 즉시 재생은 "빠른 재생" 플레이리스트(이름 고정)를 만들어 그 항목을 교체하는 방식 —
/// part1 Coordinator가 플레이리스트 단위로만 재생하므로 공개 계약을 바꾸지 않는다.
/// </summary>
public partial class HomeViewModel : ObservableObject
{
    private AppServices? _services;
    private Microsoft.UI.Dispatching.DispatcherQueue? _dispatcher;

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

    /// <summary>유튜브 계정 상태 패널 (설정과 공유하는 공용 VM — FR-15, plan T5 D5).</summary>
    public AccountPanelViewModel Account { get; } = new();

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

        // 마지막 재생 URL 복원 표시 (FR-1) — 입력 중인 값은 덮지 않는다
        if (Url.Length == 0 && services.Settings.LastHomeUrl is { Length: > 0 } lastUrl)
        {
            Url = lastUrl;
        }

        MonitorPanel.Attach(services);
        Account.Attach(services); // 진입마다 로그인 상태 재확인 (설정에서 로그아웃했을 수 있음 — plan T5)
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
        foreach (var playlist in _services.Library.Playlists)
        {
            QuickChips.Add(new QuickChip(
                playlist.Id,
                playlist.Name,
                string.Format(Loc.Get("Home_ChipCountFormat"), playlist.Items.Count)));
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

        var quickName = Loc.Get("Home_QuickPlaylistName");
        var playlist = services.Library.Playlists.FirstOrDefault(p => p.Name == quickName);
        if (playlist is null)
        {
            var created = services.Library.Create(quickName);
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

        var startResult = await services.Coordinator.StartAsync(playlist.Id);
        if (!startResult.IsSuccess)
        {
            AppLog.Write($"즉시 재생 시작 실패: {startResult.Message}");
            ToastService.Show(Loc.Get("Home_PlayFailed"), InfoBarSeverity.Error);
            return;
        }

        // 재생 성공 시에만 마지막 URL 기록 — 재실행 시 입력란 복원 표시 (FR-1)
        services.Settings.LastHomeUrl = Url.Trim();
        var saved = await services.Store.SaveSettingsAsync(services.Settings);
        if (!saved.IsSuccess)
        {
            AppLog.Write($"마지막 홈 URL 저장 실패: {saved.Message}"); // 재생은 이미 시작 — 표시 복원만 손실
        }

        RefreshChips(); // "빠른 재생" 리스트 생성·곡 교체가 칩 표시에 반영되게
        ToastService.Show(Loc.Get("Home_PlayStarted"), InfoBarSeverity.Success);
    }
}
