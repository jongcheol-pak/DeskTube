using System.Runtime.InteropServices;

namespace DeskTube.Interop;

/// <summary>
/// 프로세스 워킹셋 트림 P/Invoke (NFR-2 유휴 메모리 절감, plan D6).
/// kernel32 공식 API만 사용 — SetProcessWorkingSetSize(-1, -1)은 OS에 워킹셋 최소화를 요청한다.
/// </summary>
internal static class ProcessInterop
{
    /// <summary>현재 프로세스 의사 핸들(-1) — 닫을 필요 없음.</summary>
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

    /// <summary>
    /// 유휴 진입 시점(재생 정지·창 숨김)에 워킹셋을 OS에 반환한다 — best-effort, 실패는 로그만.
    /// 재생 중 호출 금지: 트림 직후 재생이 페이지 폴트로 느려질 수 있다 (plan D6).
    /// 전후 워킹셋을 로그로 남긴다 (NFR-2 실측 근거 + 재생 중 미호출 확인용 — T7 acceptance).
    /// </summary>
    internal static void TrimWorkingSet()
    {
        var before = Environment.WorkingSet;
        var ok = SetProcessWorkingSetSize(GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
        Services.AppLog.Write(
            $"워킹셋 트림 {(ok ? "완료" : "실패(무시)")}: {before / (1024 * 1024)}MB → {Environment.WorkingSet / (1024 * 1024)}MB");
    }
}
