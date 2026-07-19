using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel;
// AppInstance는 두 네임스페이스에 모두 존재 — AppLifecycle 쪽으로 별칭 고정 (CS0104 회피)
using AppLifecycleInstance = Microsoft.Windows.AppLifecycle.AppInstance;

namespace DeskTube.Services;

/// <summary>
/// 시작 인자 판별 — 순수 로직 (xUnit 검증 대상, plan T4 acceptance).
/// -startup 플래그는 StartupTask 활성화 감지 실패 시 폴백 + 수동 시뮬레이션 실행용.
/// </summary>
public static class StartupArgs
{
    public const string StartupFlag = "-startup";

    public static bool HasStartupFlag(IEnumerable<string?> args) =>
        args.Any(a => string.Equals(a?.Trim(), StartupFlag, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 리다이렉트로 수신한 활성화가 자동 시작 계열(조용한 시작)인지 판별 (FR-22, plan D3).
    /// 최초 인스턴스용 WasActivatedByStartupTask(GetCurrent 기반)와 달리 수신 페이로드의
    /// 종류·인자를 그대로 받는다 — StartupTask 활성화 또는 -startup 플래그(수동 시뮬레이션)면
    /// 기존 인스턴스에 영향을 주지 않고 무시할 대상이다.
    /// </summary>
    public static bool IsQuietActivation(ExtendedActivationKind kind, IEnumerable<string?> args) =>
        kind == ExtendedActivationKind.StartupTask || HasStartupFlag(args);
}

/// <summary>
/// 부팅 자동 시작 (PRD FR-8, plan T4·D3) — manifest StartupTask(DeskTubeStartupTask) 상태
/// 조회/변경 래핑 + 이 프로세스의 시작 종류 판별.
/// </summary>
public sealed class StartupService
{
    private const string TaskId = "DeskTubeStartupTask";

    public async Task<StartupTaskState> GetStateAsync()
    {
        var task = await StartupTask.GetAsync(TaskId);
        return task.State;
    }

    /// <summary>
    /// 켬/끔 요청. 반환 = 적용 후 실제 상태 — DisabledByUser면 켜기 요청이 거부된 채 유지된다
    /// (RequestEnableAsync 재요청 불가, 사용자가 Windows 설정에서 직접 켜야 함 — plan T4 Edge).
    /// 패키지형 데스크톱 앱은 동의 대화상자 없이 즉시 적용된다 (Investigation Log).
    /// </summary>
    public async Task<StartupTaskState> SetEnabledAsync(bool enable)
    {
        var task = await StartupTask.GetAsync(TaskId);
        if (enable)
        {
            return task.State == StartupTaskState.Disabled ? await task.RequestEnableAsync() : task.State;
        }

        if (task.State == StartupTaskState.Enabled)
        {
            task.Disable();
            return StartupTaskState.Disabled;
        }

        return task.State;
    }

    /// <summary>
    /// 이 프로세스가 StartupTask로 활성화됐는지 (트레이 조용 시작 판별 — plan D3).
    /// 부팅 직후에는 COM/WinRT 인프라 미준비로 조회가 실패할 수 있어, 실패 시 false를 반환하고
    /// 명령줄 인자 폴백(StartupArgs)에 판별을 맡긴다.
    /// </summary>
    public static bool WasActivatedByStartupTask()
    {
        try
        {
            return AppLifecycleInstance.GetCurrent().GetActivatedEventArgs().Kind == ExtendedActivationKind.StartupTask;
        }
        catch (Exception ex)
        {
            AppLog.Write($"활성화 종류 조회 실패(일반 시작으로 간주): {ex.GetType().Name} {ex.Message}");
            return false;
        }
    }
}
