using DeskTube.Services;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.WindowsAppRuntime;

namespace DeskTube;

/// <summary>
/// 앱 진입점. OnLaunched에서 MainWindow를 생성한다 (WinUI 3 Desktop — Window.Current 사용 금지).
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private TrayIconService? _tray;

    public App()
    {
        // 배포 초기화를 XAML 초기화(InitializeComponent)보다 먼저 수행 — 아래 메서드 주석 참조
        InitializeWindowsAppRuntime();
        InitializeComponent();
    }

    /// <summary>서비스 그래프 (컴포지션 루트 — OnLaunched에서 비동기 초기화, UI는 null 확인 후 사용).</summary>
    public static AppServices? Services { get; private set; }

    /// <summary>트레이 상주 여부 — 창 닫기를 숨김으로 바꿀지 판단 (MainWindow가 사용, plan T1).</summary>
    public static bool IsTrayActive => Current is App app && app._tray is not null;

    /// <summary>트레이 '종료' 진행 중 — 창 닫기 취소(숨김) 로직을 우회한다.</summary>
    internal static bool IsExiting { get; private set; }

    /// <summary>
    /// Services 준비 완료 알림 (UI 스레드에서 발생) — 창이 서비스보다 먼저 뜨므로,
    /// 준비 전에 열린 페이지가 이 이벤트로 늦은 초기화를 반영한다 (part2 T2).
    /// </summary>
    public static event EventHandler? ServicesInitialized;

    /// <summary>세션 프로브 컨트롤러의 부모 창 핸들 (T5 — 화면 표시 없음, 창 미생성 시 Zero).</summary>
    internal static IntPtr MainWindowHandle =>
        Current is App { _window: not null } app
            ? WinRT.Interop.WindowNative.GetWindowHandle(app._window)
            : IntPtr.Zero;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppLog.Initialize(Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "logs"));

        // 자동 시작(부팅) 판별 — 활성화 종류 우선, 조회 실패 시 명령줄 인자 폴백 (plan T4·D3)
        var quietStart = StartupService.WasActivatedByStartupTask()
            || StartupArgs.HasStartupFlag(Environment.GetCommandLineArgs());

        _window = new MainWindow();
        if (!quietStart)
        {
            _window.Activate(); // 자동 시작이면 창 없이 트레이만 (PRD FR-8)
        }

        // async void 회피 — 실패는 로그로 남기고 앱은 뜬다 (UI가 Services null로 미준비 안내)
        _ = InitializeServicesAsync(quietStart);
    }

    private async Task InitializeServicesAsync(bool autoPlay)
    {
        try
        {
            Services = await AppServices.CreateAsync(_window!.DispatcherQueue);
        }
        catch (Exception ex)
        {
            AppLog.Write($"서비스 초기화 실패: {ex.GetType().Name} {ex.Message}");
            return; // 트레이 미생성 — 이 경우 창 닫기는 실제 종료로 동작한다 (MainWindow 참조)
        }

        // await 후에도 UI 스레드 컨텍스트 (WinUI SynchronizationContext) — 트레이는 UI 스레드에서 생성
        _tray = new TrayIconService(Services, ShowMainWindow, ExitApplication);
        _tray.Initialize();

        ServicesInitialized?.Invoke(this, EventArgs.Empty);

        if (autoPlay)
        {
            await TryAutoPlayLastAsync();
        }
    }

    /// <summary>자동 시작 — 마지막 재생 리스트로 조용히 재생 시작 (PRD Q8). 리스트가 없으면 생략하고 트레이만 상주 (plan T4 Edge).</summary>
    private async Task TryAutoPlayLastAsync()
    {
        var services = Services!;
        var lastId = services.Settings.LastPlaylistId;
        var playlist = lastId.HasValue
            ? services.Library.Playlists.FirstOrDefault(p => p.Id == lastId.Value)
            : null;
        if (playlist is null || playlist.Items.Count == 0)
        {
            AppLog.Write("자동 시작: 재생할 마지막 플레이리스트가 없어 재생을 생략합니다.");
            return;
        }

        var result = await services.Coordinator.StartAsync(playlist.Id);
        if (!result.IsSuccess)
        {
            AppLog.Write($"자동 시작 재생 실패: {result.Message}");
        }
    }

    /// <summary>설정 창 표시 — 트레이 메뉴·더블클릭 진입점 (notice가 있으면 InfoBar 안내 표시).</summary>
    internal void ShowMainWindow(string? notice)
    {
        if (_window is not MainWindow window)
        {
            return;
        }

        window.AppWindow.Show();
        window.Activate();
        if (notice is not null)
        {
            window.ShowNotice(notice);
        }
    }

    /// <summary>트레이 '종료' — 재생 정리·배경 복구(Services.Dispose) 후 앱 종료 (plan T1 Edge).</summary>
    internal void ExitApplication()
    {
        IsExiting = true;
        _tray?.Dispose();
        _tray = null;
        Services?.Dispose();
        Services = null;
        Exit();
    }

    /// <summary>
    /// Windows App Runtime 배포 초기화 (실패 처리·옵션은 SDK 원본 AutoInitializer와 동일:
    /// OnErrorShowUI + 실패 시 HResult로 종료).
    /// 단 실행 시점은 원본(module initializer — 어셈블리 타입 최초 접근 전)보다 늦은 App 생성자다
    /// — 테스트 호스트가 앱 어셈블리를 로드할 때 초기화가 실행돼 죽는 문제(0x80040154) 때문에
    /// csproj에서 자동 초기화를 끄고 여기서 호출한다. 프레임워크 패키지의 최초 설치/복구
    /// 시나리오(클린 설치 후 첫 실행)는 실기 HUMAN-VERIFY 대상 (plan Verification Strategy).
    /// </summary>
    private static void InitializeWindowsAppRuntime()
    {
        var result = DeploymentManager.Initialize(new DeploymentInitializeOptions { OnErrorShowUI = true });
        if (result.Status != DeploymentStatus.Ok)
        {
            var hr = result.ExtendedError.HResult;
            Environment.Exit(hr);
        }
    }
}
