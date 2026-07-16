using System.Runtime.InteropServices;

namespace DeskTube.Interop;

/// <summary>
/// 배경 재생용 순수 Win32 표시 창 (plan 2026-07-16 D1·D2).
/// WinUI 3 창은 크로스 프로세스 SetParent(WorkerW)를 지원하지 않아 네이티브 AV로 죽으므로
/// (docs/debug-2026-07-16-av-crash.md), 배경 표면은 XAML 없이 이 창을 쓰고
/// WebView2는 CoreWebView2Controller로 이 창의 HWND에 호스팅한다 (PlayerHost).
/// UI 스레드에서 생성·파괴하는 전제 — 메시지는 앱의 기존 메시지 루프가 펌핑한다.
/// </summary>
internal sealed class WallpaperSurface : IDisposable
{
    private const uint WmSize = 0x0005;
    private const long WsPopup = unchecked((long)0x80000000L);
    private const uint WsExToolWindow = 0x00000080;   // Alt-Tab 미노출 (위키 오버레이 관행)
    private const uint WsExNoActivate = 0x08000000;   // 포커스 훔치지 않음
    private const int BlackBrush = 4;                 // GetStockObject — 로드 전 검정 배경 (깜빡임 방지)

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

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassW(ref WNDCLASSW wndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClassW(string className, IntPtr instance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint exStyle, string className, string? windowName, uint style,
        int x, int y, int width, int height,
        IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? moduleName);

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetStockObject(int index);

    /// <summary>WndProc 델리게이트를 필드로 보유해 GC 수거를 방지한다 (MonitorInterop과 동일 이유).</summary>
    private readonly WndProc _wndProc;
    private readonly string _className;
    private readonly IntPtr _instance;

    public WallpaperSurface(int x, int y, int width, int height)
    {
        _wndProc = HandleMessage;

        var className = "DeskTubeWallpaperSurface_" + Guid.NewGuid().ToString("N");
        var wndClass = new WNDCLASSW
        {
            lpfnWndProc = _wndProc,
            hInstance = GetModuleHandleW(null),
            hbrBackground = GetStockObject(BlackBrush),
            lpszClassName = className,
        };

        if (RegisterClassW(ref wndClass) == 0)
        {
            throw new InvalidOperationException($"배경창 클래스 등록 실패 (Win32 오류 {Marshal.GetLastWin32Error()})");
        }

        _className = className;
        _instance = wndClass.hInstance;

        Hwnd = CreateWindowExW(
            WsExToolWindow | WsExNoActivate, className, null, unchecked((uint)WsPopup),
            x, y, width, height,
            IntPtr.Zero, IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);
        if (Hwnd == IntPtr.Zero)
        {
            UnregisterClassW(_className, _instance); // 생성 실패 시 등록 잔재 제거
            throw new InvalidOperationException($"배경창 생성 실패 (Win32 오류 {Marshal.GetLastWin32Error()})");
        }
    }

    public IntPtr Hwnd { get; private set; }

    /// <summary>클라이언트 크기 변경 알림 (width, height) — PlayerHost가 컨트롤러 Bounds를 추종 (plan D3).</summary>
    public event Action<int, int>? Resized;

    /// <summary>현재 클라이언트 크기 (파괴 후에는 0,0).</summary>
    public (int Width, int Height) ClientSize
    {
        get
        {
            if (Hwnd != IntPtr.Zero && GetClientRect(Hwnd, out var rect))
            {
                return (rect.Right - rect.Left, rect.Bottom - rect.Top);
            }

            return (0, 0);
        }
    }

    private IntPtr HandleMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmSize && Hwnd != IntPtr.Zero)
        {
            try
            {
                // lParam: LOWORD=클라이언트 너비, HIWORD=높이
                var size = lParam.ToInt64();
                Resized?.Invoke((int)(size & 0xFFFF), (int)((size >> 16) & 0xFFFF));
            }
            catch (Exception ex)
            {
                // reverse P/Invoke 콜백에서 예외가 새면 프로세스가 죽는다 — 로그만 남기고 삼킴
                Services.AppLog.Write($"배경창 크기 콜백 오류: {ex.GetType().Name} {ex.Message}");
            }
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (Hwnd != IntPtr.Zero)
        {
            DestroyWindow(Hwnd);
            Hwnd = IntPtr.Zero;
            UnregisterClassW(_className, _instance); // 아톰 테이블 누적 방지 (MonitorInterop과 동일)
        }
    }
}
