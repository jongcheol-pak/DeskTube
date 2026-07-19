using DeskTube.Interop;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
// AppInstance는 두 네임스페이스에 모두 존재 — AppLifecycle 쪽으로 별칭 고정 (CS0104 회피, StartupService 관례)
using AppLifecycleInstance = Microsoft.Windows.AppLifecycle.AppInstance;

namespace DeskTube;

/// <summary>
/// 커스텀 진입점 (csproj DISABLE_XAML_GENERATED_MAIN) — 단일 인스턴스 게이트 (FR-22).
/// XAML·App 생성 전에 판정해, 두 번째 프로세스는 창·리소스를 만들지 않고
/// 기존 인스턴스로 활성화를 위임한 뒤 조용히 종료한다 (plan D2).
/// </summary>
public static class Program
{
    /// <summary>
    /// 게이트 실패 폴백 사유 — 이 시점엔 AppLog가 미초기화(OnLaunched에서 초기화)라
    /// 사유만 보관하고 App.OnLaunched가 기록한다 (plan T2 D4).
    /// </summary>
    internal static string? SingleInstanceFallbackReason { get; private set; }

    [STAThread]
    private static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (TryRedirectToExistingInstance())
        {
            return; // 기존 인스턴스에 위임 완료 — 조용히 종료
        }

        // 이하 생성 Main(App.g.i.cs)과 동일 — 상수 정의로 컴파일 제외된 본문을 승계
        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }

    /// <summary>
    /// 이미 실행 중인 인스턴스가 있으면 활성화 인자를 그대로 넘기고 true(이 프로세스 종료).
    /// 활성화 인자가 전달되므로 기존 인스턴스가 자동실행(StartupTask) 중복 여부를 판별할 수 있다.
    /// 게이트 실패는 일반 시작으로 폴백 — 최악이 "종전과 동일(중복 실행)"이고,
    /// 조용한 종료 폴백은 "앱이 안 뜬다"는 더 나쁜 무반응이다 (plan D4).
    /// </summary>
    private static bool TryRedirectToExistingInstance()
    {
        AppActivationArguments activationArgs;
        AppLifecycleInstance keyInstance;
        try
        {
            activationArgs = AppLifecycleInstance.GetCurrent().GetActivatedEventArgs();
            keyInstance = AppLifecycleInstance.FindOrRegisterForKey("main"); // 키는 사용자별 격리 — plan D6
        }
        catch (Exception ex)
        {
            // 부팅 초기 COM/WinRT 미준비 등 (StartupService.WasActivatedByStartupTask 폴백 관례와 동일 방향)
            SingleInstanceFallbackReason = $"단일 인스턴스 게이트 조회 실패: {ex.GetType().Name} {ex.Message}";
            return false;
        }

        if (keyInstance.IsCurrent)
        {
            return false; // 최초 인스턴스 — 정상 기동 계속
        }

        // 기존 인스턴스에 전면화 권한 위양 (plan D5) — 실패해도 창 표시는 별도 성립하므로 무시
        _ = ActivationInterop.AllowSetForegroundWindow(keyInstance.ProcessId);

        try
        {
            ActivationInterop.WaitOnStaThread(keyInstance.RedirectActivationToAsync(activationArgs).AsTask());
            return true;
        }
        catch (Exception ex)
        {
            // 기존 인스턴스 소멸 직후 race 등 — 일반 시작 계속 (plan D4)
            SingleInstanceFallbackReason = $"활성화 리다이렉트 실패(일반 시작 폴백): {ex.GetType().Name} {ex.Message}";
            return false;
        }
    }
}
