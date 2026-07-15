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
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppLog.Initialize(Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "logs"));
        InitializeWindowsAppRuntime();

        _window = new MainWindow();
        _window.Activate();
    }

    /// <summary>
    /// Windows App Runtime 배포 초기화.
    /// csproj에서 모듈 자동 초기화를 껐으므로(테스트 호스트 호환 — csproj 주석 참조)
    /// SDK의 DeploymentManagerAutoInitializer와 동일한 동작을 여기서 명시 수행한다.
    /// </summary>
    private static void InitializeWindowsAppRuntime()
    {
        var result = DeploymentManager.Initialize(new DeploymentInitializeOptions { OnErrorShowUI = true });
        if (result.Status != DeploymentStatus.Ok)
        {
            var hr = result.ExtendedError.HResult;
            AppLog.Write($"Windows App Runtime 초기화 실패: 0x{hr:X8}");
            Environment.Exit(hr);
        }
    }
}
