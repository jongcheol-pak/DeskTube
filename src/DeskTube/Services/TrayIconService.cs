using CommunityToolkit.Mvvm.Input;
using DeskTube.Models;
using H.NotifyIcon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DeskTube.Services;

/// <summary>
/// 트레이 아이콘 (PRD FR-9, plan T1·D2 — 2026-07-17 토글 2항목 재구성).
/// App 수명으로 소유 — 설정 창이 닫혀도(숨김) 트레이 메뉴로 앱을 제어한다.
/// 메뉴는 재생/정지·음소거의 상태 연동 토글 2항목 + 설정 열기·종료.
/// 렌더링은 PopupMenu(Win32 네이티브) 모드 — SecondWindow(preview)의 스크롤·
/// 동적 문구 미반영 버그(H.NotifyIcon #21/#97)를 원천 회피한다 (plan D6).
/// 문구 갱신은 Opening이 아니라 StatusChanged/MutedChanged 이벤트 시점에 선반영
/// — 메뉴를 열기 전에 항상 최신 문구가 준비된다.
/// 셸 재시작(TaskbarCreated) 시 아이콘 재등록은 H.NotifyIcon이 자동 처리한다.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly AppServices _services;
    private readonly Action<string?> _showSettings;
    private readonly Action _exitApp;

    private TaskbarIcon? _icon;
    private MenuFlyoutItem? _playStopItem;
    private MenuFlyoutItem? _muteItem;
    private DispatcherQueue? _dispatcher;

    /// <param name="showSettings">설정 창 표시 (인자: 표시할 안내 메시지, null이면 창만)</param>
    /// <param name="exitApp">앱 종료 (배경 복구 포함 정리는 호출측 책임)</param>
    public TrayIconService(AppServices services, Action<string?> showSettings, Action exitApp)
    {
        _services = services;
        _showSettings = showSettings;
        _exitApp = exitApp;
    }

    /// <summary>UI 스레드에서 호출 — 메뉴 구성 후 트레이 아이콘을 즉시 등록한다.</summary>
    public void Initialize()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // PopupMenu(네이티브) 모드는 항목 선택 시 Command만 실행하고 WinUI Click 이벤트를 발생시키지
        // 않는다 (H.NotifyIcon 2.4.1 PopulateMenu — Command?.TryExecute). 반드시 Command로 연결할 것.
        var menu = new MenuFlyout();

        _playStopItem = new MenuFlyoutItem { Command = new AsyncRelayCommand(TogglePlayStopAsync) };
        menu.Items.Add(_playStopItem);

        _muteItem = new MenuFlyoutItem { Command = new AsyncRelayCommand(ToggleMuteAsync) };
        menu.Items.Add(_muteItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        var settings = new MenuFlyoutItem
        {
            Text = Loc.Get("Tray_OpenSettings"),
            Command = new RelayCommand(() => _showSettings(null)),
        };
        menu.Items.Add(settings);

        var exit = new MenuFlyoutItem
        {
            Text = Loc.Get("Tray_Exit"),
            Command = new RelayCommand(_exitApp),
        };
        menu.Items.Add(exit);

        // 상태 연동 문구 — 초기값은 현재 상태로, 이후는 이벤트가 갱신 (Dispose에서 해제)
        UpdatePlayStopText();
        UpdateMuteText();
        _services.Coordinator.StatusChanged += OnStatusChanged;
        _services.Coordinator.MutedChanged += OnMutedChanged;

        _icon = new TaskbarIcon
        {
            ToolTipText = Loc.Get("Tray_ToolTip"),
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon.ico")),
            ContextMenuMode = ContextMenuMode.PopupMenu,
            // MenuActivation 기본값이 RightClick — 명시 불필요
            DoubleClickCommand = new RelayCommand(() => _showSettings(null)),
            ContextFlyout = menu,
        };
        _icon.ForceCreate();
    }

    /// <summary>재생 상태 변경 — 발생 스레드가 보장되지 않아 UI로 마셜링 후 문구 갱신.</summary>
    private void OnStatusChanged(object? sender, PlaybackStatus status) =>
        _dispatcher?.TryEnqueue(UpdatePlayStopText);

    private void OnMutedChanged(object? sender, EventArgs e) =>
        _dispatcher?.TryEnqueue(UpdateMuteText);

    /// <summary>재생 중이면 "정지", 정지·일시정지면 "재생" (FR-9 상태 연동 문구).</summary>
    private void UpdatePlayStopText()
    {
        if (_playStopItem is not null)
        {
            _playStopItem.Text = Loc.Get(
                _services.Coordinator.Status == PlaybackStatus.Playing ? "Tray_Stop" : "Tray_Play");
        }
    }

    /// <summary>비음소거면 "음소거", 음소거면 "음소거 해제" (FR-9 상태 연동 문구).</summary>
    private void UpdateMuteText()
    {
        if (_muteItem is not null)
        {
            _muteItem.Text = Loc.Get(_services.Settings.IsMuted ? "Tray_Unmute" : "Tray_Mute");
        }
    }

    private async Task TogglePlayStopAsync()
    {
        try
        {
            if (_services.Coordinator.Status == PlaybackStatus.Playing)
            {
                await _services.Coordinator.StopAsync();
            }
            else
            {
                await PlayAsync();
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"트레이 재생/정지 처리 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }

    private async Task ToggleMuteAsync()
    {
        try
        {
            // 문구 갱신은 SetMutedAsync가 발화하는 MutedChanged 구독이 처리
            await _services.Coordinator.SetMutedAsync(!_services.Settings.IsMuted);
        }
        catch (Exception ex)
        {
            AppLog.Write($"트레이 음소거 전환 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }

    /// <summary>
    /// 재생 — 일시정지면 재개, 정지면 마지막 플레이리스트를 마지막 항목부터 재생 (FR-19 재개 방식 통일, plan D8).
    /// 재생할 리스트가 없으면 조용히 무시하지 않고 설정 창을 열어 안내한다 (plan T1 Edge).
    /// </summary>
    private async Task PlayAsync()
    {
        var coordinator = _services.Coordinator;
        if (coordinator.Status == PlaybackStatus.Playing)
        {
            return;
        }

        if (coordinator.Status == PlaybackStatus.Paused)
        {
            coordinator.Resume();
            return;
        }

        // 마지막 리스트 재개는 앱 자동 시작과 공용 경로 (StartLastAsync — 중복 구현 제거, 2026-07-17)
        var result = await coordinator.StartLastAsync();
        if (!result.IsSuccess)
        {
            if (result.Code == ErrorCode.NotFound)
            {
                _showSettings(Loc.Get("Tray_NoPlaylist")); // 재생할 리스트 없음 — 조용히 무시하지 않고 안내
            }
            else
            {
                // 서비스 오류 문구는 미로컬라이즈(내부용) — 사용자에겐 리소스 문구, 상세는 로그로
                AppLog.Write($"트레이 재생 시작 실패: {result.Message}");
                _showSettings(Loc.Get("Tray_PlayFailed"));
            }
        }
    }

    public void Dispose()
    {
        _services.Coordinator.StatusChanged -= OnStatusChanged;
        _services.Coordinator.MutedChanged -= OnMutedChanged;
        _icon?.Dispose();
        _icon = null;
    }
}
