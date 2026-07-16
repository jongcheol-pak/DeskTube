using System.Runtime.InteropServices;

namespace DeskTube.Interop;

/// <summary>
/// 자동 일시정지 신호용 P/Invoke (NFR-1, plan D6·D7).
/// - 전체화면 감지: SHQueryUserNotificationState (shell32 공식 API)
/// - 세션 잠금 감지: WTSRegisterSessionNotification + WM_WTSSESSION_CHANGE 메시지 전용 창
/// </summary>
internal static class SessionInterop
{
    // QUERY_USER_NOTIFICATION_STATE — 전체화면으로 판정하는 상태 (plan D6)
    private const int QunsBusy = 2;                  // 전체화면 앱 (F11 등)
    private const int QunsRunningD3dFullScreen = 3;  // D3D 독점 전체화면 (게임)
    private const int QunsPresentationMode = 4;      // 프레젠테이션 모드

    private const uint WmWtsSessionChange = 0x02B1;
    private const int WtsSessionLock = 0x7;
    private const int WtsSessionUnlock = 0x8;
    private const int NotifyForThisSession = 0;

    private static readonly IntPtr HwndMessage = new(-3);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSRegisterSessionNotification(IntPtr hwnd, int flags);

    [DllImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr hwnd);

    /// <summary>포그라운드에 전체화면 앱(게임·프레젠테이션)이 있는지 판정한다 (2초 폴링 대상 — D6).</summary>
    internal static bool IsFullscreenAppActive()
    {
        // 실패(HRESULT != S_OK) 시 보수적으로 false — 오탐으로 배경 재생을 멈추지 않는다
        return SHQueryUserNotificationState(out var state) == 0 &&
               state is QunsBusy or QunsRunningD3dFullScreen or QunsPresentationMode;
    }

    /// <summary>
    /// 세션 잠금/해제 알림 수신 창. WndProc 델리게이트를 필드로 보유해 GC 수거를 방지한다
    /// (MonitorInterop.DisplayChangeWindow와 동일 패턴 — 메시지·등록 대상이 달라 별도 구현).
    /// </summary>
    internal sealed class SessionLockWindow : IDisposable
    {
        private delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSW
        {
            public uint style;
            public WndProc lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassW(ref WNDCLASSW wndClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowExW(
            uint exStyle, string className, string? windowName, uint style,
            int x, int y, int width, int height,
            IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterClassW(string className, IntPtr instance);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string? moduleName);

        private readonly WndProc _wndProc;
        private readonly Action<bool> _onLockChanged;
        private readonly string _className;
        private readonly IntPtr _instance;
        private IntPtr _hwnd;

        /// <param name="onLockChanged">true = 잠금, false = 해제.</param>
        internal SessionLockWindow(Action<bool> onLockChanged)
        {
            _onLockChanged = onLockChanged;
            _wndProc = HandleMessage;

            var className = "DeskTubeSessionLock_" + Guid.NewGuid().ToString("N");
            var wndClass = new WNDCLASSW
            {
                lpfnWndProc = _wndProc,
                hInstance = GetModuleHandleW(null),
                lpszClassName = className,
            };

            if (RegisterClassW(ref wndClass) == 0)
            {
                throw new InvalidOperationException($"세션 알림 창 클래스 등록 실패 (Win32 오류 {Marshal.GetLastWin32Error()})");
            }

            _className = className;
            _instance = wndClass.hInstance;

            _hwnd = CreateWindowExW(0, className, null, 0, 0, 0, 0, 0, HwndMessage, IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
            {
                UnregisterClassW(_className, _instance);
                throw new InvalidOperationException($"세션 알림 창 생성 실패 (Win32 오류 {Marshal.GetLastWin32Error()})");
            }

            if (!WTSRegisterSessionNotification(_hwnd, NotifyForThisSession))
            {
                var error = Marshal.GetLastWin32Error();
                Dispose();
                throw new InvalidOperationException($"세션 알림 등록 실패 (Win32 오류 {error})");
            }
        }

        private IntPtr HandleMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WmWtsSessionChange)
            {
                var eventCode = wParam.ToInt32();
                if (eventCode is WtsSessionLock or WtsSessionUnlock)
                {
                    try
                    {
                        _onLockChanged(eventCode == WtsSessionLock);
                    }
                    catch (Exception ex)
                    {
                        // reverse P/Invoke 콜백에서 예외가 새면 프로세스가 죽는다 — 로그만 남기고 삼킴
                        Services.AppLog.Write($"세션 잠금 콜백 오류: {ex.GetType().Name} {ex.Message}");
                    }
                }
            }

            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hwnd != IntPtr.Zero)
            {
                WTSUnRegisterSessionNotification(_hwnd);
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
                UnregisterClassW(_className, _instance);
            }
        }
    }
}
