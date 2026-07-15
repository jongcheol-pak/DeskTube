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

    public App()
    {
        // 배포 초기화를 XAML 초기화(InitializeComponent)보다 먼저 수행 — 아래 메서드 주석 참조
        InitializeWindowsAppRuntime();
        InitializeComponent();
    }

    /// <summary>서비스 그래프 (컴포지션 루트 — OnLaunched에서 비동기 초기화, UI는 null 확인 후 사용).</summary>
    public static AppServices? Services { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppLog.Initialize(Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "logs"));

        _window = new MainWindow();
        _window.Activate();

        // async void 회피 — 실패는 로그로 남기고 앱은 뜬다 (UI가 Services null로 미준비 안내)
        _ = InitializeServicesAsync();
    }

    private async Task InitializeServicesAsync()
    {
        try
        {
            Services = await AppServices.CreateAsync(_window!.DispatcherQueue);
        }
        catch (Exception ex)
        {
            AppLog.Write($"서비스 초기화 실패: {ex.GetType().Name} {ex.Message}");
        }
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
