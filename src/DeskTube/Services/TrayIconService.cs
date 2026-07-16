using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DeskTube.Services;

/// <summary>
/// 트레이 아이콘 (PRD FR-9, plan T1·D2). App 수명으로 소유 — 설정 창이 닫혀도(숨김)
/// 트레이 메뉴(재생/정지/볼륨/설정 열기/종료)로 앱을 제어한다.
/// 셸 재시작(TaskbarCreated) 시 아이콘 재등록은 H.NotifyIcon이 자동 처리한다.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly AppServices _services;
    private readonly Action<string?> _showSettings;
    private readonly Action _exitApp;

    private TaskbarIcon? _icon;
    private ToggleMenuFlyoutItem? _volumeItem;

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
        var menu = new MenuFlyout();

        var play = new MenuFlyoutItem { Text = Loc.Get("Tray_Play") };
        play.Click += OnPlayClick;
        menu.Items.Add(play);

        var stop = new MenuFlyoutItem { Text = Loc.Get("Tray_Stop") };
        stop.Click += OnStopClick;
        menu.Items.Add(stop);

        _volumeItem = new ToggleMenuFlyoutItem { Text = Loc.Get("Tray_Volume") };
        _volumeItem.Click += OnVolumeClick;
        menu.Items.Add(_volumeItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        var settings = new MenuFlyoutItem { Text = Loc.Get("Tray_OpenSettings") };
        settings.Click += (_, _) => _showSettings(null);
        menu.Items.Add(settings);

        var exit = new MenuFlyoutItem { Text = Loc.Get("Tray_Exit") };
        exit.Click += (_, _) => _exitApp();
        menu.Items.Add(exit);

        // 열릴 때마다 현재 설정으로 체크 상태 동기화 (설정 화면에서 바뀔 수 있음)
        menu.Opening += (_, _) => _volumeItem.IsChecked = !_services.Settings.IsMuted;

        _icon = new TaskbarIcon
        {
            ToolTipText = Loc.Get("Tray_ToolTip"),
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/tray.ico")),
            ContextMenuMode = ContextMenuMode.SecondWindow,
            // MenuActivation 기본값이 RightClick — 명시 불필요
            DoubleClickCommand = new RelayCommand(() => _showSettings(null)),
            ContextFlyout = menu,
        };
        _icon.ForceCreate();
    }

    private async void OnPlayClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            await PlayAsync();
        }
        catch (Exception ex)
        {
            AppLog.Write($"트레이 재생 처리 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }

    private async void OnStopClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            await _services.Coordinator.StopAsync();
        }
        catch (Exception ex)
        {
            AppLog.Write($"트레이 정지 처리 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }

    private async void OnVolumeClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var mute = !_services.Settings.IsMuted;
            await _services.Coordinator.SetMutedAsync(mute);
            if (_volumeItem is not null)
            {
                _volumeItem.IsChecked = !mute;
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"트레이 볼륨 전환 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }

    /// <summary>
    /// 재생 — 일시정지면 재개, 정지면 마지막 플레이리스트 재생.
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

        var lastId = _services.Settings.LastPlaylistId;
        var playlist = lastId.HasValue
            ? _services.Library.Playlists.FirstOrDefault(p => p.Id == lastId.Value)
            : null;
        if (playlist is null || playlist.Items.Count == 0)
        {
            _showSettings(Loc.Get("Tray_NoPlaylist"));
            return;
        }

        var result = await coordinator.StartAsync(playlist.Id);
        if (!result.IsSuccess)
        {
            _showSettings(result.Message ?? Loc.Get("Tray_PlayFailed"));
        }
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }
}
