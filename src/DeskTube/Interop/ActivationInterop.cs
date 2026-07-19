using System.Runtime.InteropServices;

namespace DeskTube.Interop;

/// <summary>
/// 단일 인스턴스 활성화 P/Invoke (FR-22, plan D5·D9).
/// 전면화 권한 위양(user32)과 STA 안전 리다이렉트 대기(kernel32/ole32)만 담당한다.
/// </summary>
internal static class ActivationInterop
{
    /// <summary>
    /// 지정 프로세스에 전면화 권한을 위양한다 — 백그라운드 프로세스(기존 인스턴스)는
    /// 스스로 전면화할 수 없으므로, 실행 주체(이 프로세스)가 권한을 넘겨야
    /// 기존 인스턴스의 SetForegroundWindow가 성립한다 (plan D5). 실패는 무시(best-effort).
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AllowSetForegroundWindow(uint dwProcessId);

    /// <summary>리다이렉트 수신 시 기존 인스턴스가 자기 창을 전면화한다 — 실패는 무시(best-effort).</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateEventW(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetEvent(IntPtr hEvent);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("ole32.dll")]
    private static extern uint CoWaitForMultipleObjects(uint dwFlags, uint dwTimeout, uint cHandles, IntPtr[] pHandles, out uint lpdwindex);

    /// <summary>
    /// STA 메인 스레드에서 비동기 작업(리다이렉트)을 교착 없이 완료 대기한다 (plan D9).
    /// STA에서 Task.Wait 직접 블로킹은 COM 마셜링 교착 위험이 있어, 백그라운드에서
    /// 완료를 알리는 이벤트 핸들을 CoWaitForMultipleObjects(메시지 펌핑 대기)로 기다린다
    /// — MS 공식 단일 인스턴스 샘플 패턴. 작업 실패는 호출부로 다시 던진다(폴백 판단용).
    /// </summary>
    internal static void WaitOnStaThread(Task task)
    {
        var eventHandle = CreateEventW(IntPtr.Zero, bManualReset: true, bInitialState: false, lpName: null);
        if (eventHandle == IntPtr.Zero)
        {
            task.Wait(); // 이벤트 생성 실패(희귀) — 직접 대기 폴백
            return;
        }

        try
        {
            Exception? failure = null;
            _ = Task.Run(() =>
            {
                try
                {
                    task.Wait();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                SetEvent(eventHandle);
            });

            _ = CoWaitForMultipleObjects(0, 0xFFFFFFFF, 1, [eventHandle], out _);
            if (failure is not null)
            {
                throw failure;
            }
        }
        finally
        {
            CloseHandle(eventHandle);
        }
    }
}
