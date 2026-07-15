using System.Runtime.InteropServices;

namespace DeskTube.Interop;

/// <summary>
/// 모니터 열거·표시 변경 감지 P/Invoke (PRD FR-3, plan T4).
/// user32 공식 API만 사용 — EnumDisplayMonitors / GetMonitorInfoW / 메시지 전용 창(WM_DISPLAYCHANGE).
/// </summary>
internal static class MonitorInterop
{
    private const int MonitorinfofPrimary = 0x00000001;
    private const uint WmDisplayChange = 0x007E;
    private static readonly IntPtr HwndMessage = new(-3); // HWND_MESSAGE — 메시지 전용 창 부모

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEXW
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    /// <summary>열거 결과 원시 데이터 (서비스 레이어가 MonitorInfo로 변환).</summary>
    internal readonly record struct RawMonitor(string DeviceName, RECT Bounds, bool IsPrimary);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFOEXW info);

    /// <summary>연결된 모든 모니터를 열거한다. 실패한 항목은 건너뛴다.</summary>
    internal static List<RawMonitor> EnumerateMonitors()
    {
        var monitors = new List<RawMonitor>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var info = new MONITORINFOEXW { cbSize = Marshal.SizeOf<MONITORINFOEXW>() };
            if (GetMonitorInfoW(hMonitor, ref info))
            {
                monitors.Add(new RawMonitor(
                    info.szDevice,
                    info.rcMonitor,
                    (info.dwFlags & MonitorinfofPrimary) != 0));
            }

            return true; // 계속 열거
        }, IntPtr.Zero);

        return monitors;
    }

    // ---- 메시지 전용 창 (WM_DISPLAYCHANGE 수신) ----

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

    /// <summary>
    /// WM_DISPLAYCHANGE를 수신하는 메시지 전용 창.
    /// WndProc 델리게이트를 필드로 보유해 GC 수거를 방지한다 (수거되면 네이티브 콜백이 죽음).
    /// </summary>
    internal sealed class DisplayChangeWindow : IDisposable
    {
        private readonly WndProc _wndProc;
        private readonly Action _onDisplayChange;
        private readonly string _className;
        private readonly IntPtr _instance;
        private IntPtr _hwnd;

        internal DisplayChangeWindow(Action onDisplayChange)
        {
            _onDisplayChange = onDisplayChange;
            _wndProc = HandleMessage;

            var className = "DeskTubeDisplayChange_" + Guid.NewGuid().ToString("N");
            var wndClass = new WNDCLASSW
            {
                lpfnWndProc = _wndProc,
                hInstance = GetModuleHandleW(null),
                lpszClassName = className,
            };

            if (RegisterClassW(ref wndClass) == 0)
            {
                throw new InvalidOperationException($"메시지 창 클래스 등록 실패 (Win32 오류 {Marshal.GetLastWin32Error()})");
            }

            _className = className;
            _instance = wndClass.hInstance;

            _hwnd = CreateWindowExW(0, className, null, 0, 0, 0, 0, 0, HwndMessage, IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
            {
                UnregisterClassW(_className, _instance); // 창 생성 실패 시에도 등록 잔재를 남기지 않음
                throw new InvalidOperationException($"메시지 창 생성 실패 (Win32 오류 {Marshal.GetLastWin32Error()})");
            }
        }

        private IntPtr HandleMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WmDisplayChange)
            {
                try
                {
                    _onDisplayChange();
                }
                catch (Exception ex)
                {
                    // reverse P/Invoke 콜백에서 예외가 새면 프로세스가 FailFast로 죽는다 — 로그만 남기고 삼킴
                    Services.AppLog.Write($"모니터 변경 콜백 오류: {ex.GetType().Name} {ex.Message}");
                }
            }

            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
                // RegisterClassW 대응 해제 — 반복 생성/폐기 시 아톰 테이블 누적 방지
                UnregisterClassW(_className, _instance);
            }
        }
    }
}
