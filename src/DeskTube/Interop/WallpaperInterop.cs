using System.Runtime.InteropServices;

namespace DeskTube.Interop;

/// <summary>
/// 바탕화면 아이콘 뒤(WorkerW) 창 부착 P/Invoke (PRD FR-2, plan D3).
/// Windows 11 24H2 전후의 WorkerW 계층 차이를 이중 경로로 지원한다:
///   경로 1 (24H2+): WorkerW가 Progman의 자식으로 생성됨
///   경로 2 (이전): SHELLDLL_DefView를 가진 창의 "다음 형제" WorkerW
/// </summary>
internal static class WallpaperInterop
{
    /// <summary>Progman에 보내면 아이콘 뒤 WorkerW 생성을 유도하는 비공개 메시지 (Lively 등 검증된 관행).</summary>
    private const uint SpawnWorkerWMessage = 0x052C;

    private const uint SmtoNormal = 0x0000;
    private const int GwlStyle = -16;

    private const long WsChild = 0x40000000L;
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsSysMenu = 0x00080000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const long WsPopup = unchecked((long)0x80000000L);

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int SwShowNoActivate = 4;

    private const uint SpiSetDeskWallpaper = 0x0014;
    private const uint SpifUpdateIniFile = 0x01;
    private const uint SpifSendChange = 0x02;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowExW(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeoutW(
        IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeoutMs, out IntPtr result);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr newValue);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hwnd, int cmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(IntPtr hwnd, ref POINT point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoW(uint action, uint uiParam, string? pvParam, uint winIni);

    /// <summary>
    /// 아이콘 뒤 WorkerW 핸들을 찾는다 (없으면 IntPtr.Zero).
    /// Progman에 0x052C를 보내 WorkerW 생성을 유도한 뒤 신·구 두 경로로 탐색한다.
    /// </summary>
    internal static IntPtr FindWorkerW()
    {
        var progman = FindWindowW("Progman", null);
        if (progman == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        // 구형(파라미터 0,0)·신형(0xD,0x1) 파라미터를 모두 전송 — 어느 쪽이든 무해
        SendMessageTimeoutW(progman, SpawnWorkerWMessage, IntPtr.Zero, IntPtr.Zero, SmtoNormal, 1000, out _);
        SendMessageTimeoutW(progman, SpawnWorkerWMessage, new IntPtr(0xD), new IntPtr(0x1), SmtoNormal, 1000, out _);

        // 경로 1 (24H2+): Progman 자식 WorkerW
        var childWorkerW = FindWindowExW(progman, IntPtr.Zero, "WorkerW", null);
        if (childWorkerW != IntPtr.Zero)
        {
            return childWorkerW;
        }

        // 경로 2 (이전): SHELLDLL_DefView를 가진 최상위 창의 다음 형제 WorkerW
        var siblingWorkerW = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            var shellView = FindWindowExW(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
            {
                var next = FindWindowExW(IntPtr.Zero, hwnd, "WorkerW", null);
                if (next != IntPtr.Zero)
                {
                    siblingWorkerW = next;
                    return false; // 탐색 종료
                }
            }

            return true;
        }, IntPtr.Zero);

        return siblingWorkerW;
    }

    /// <summary>
    /// 창을 WorkerW의 자식으로 만들어 아이콘 뒤에 배치한다.
    /// 스타일에서 캡션·테두리를 제거하고 WS_CHILD로 전환 후 모니터 영역(물리 픽셀)에 맞춘다.
    /// </summary>
    internal static void AttachToWorkerW(IntPtr hwnd, IntPtr workerW, int screenX, int screenY, int width, int height)
    {
        var style = GetWindowLongPtrW(hwnd, GwlStyle).ToInt64();
        style &= ~(WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox | WsPopup);
        style |= WsChild;
        SetWindowLongPtrW(hwnd, GwlStyle, new IntPtr(style));

        SetParent(hwnd, workerW);
        PositionOnWorkerW(hwnd, workerW, screenX, screenY, width, height);
        ShowWindow(hwnd, SwShowNoActivate); // 포커스 훔치지 않고 표시
    }

    /// <summary>모니터 화면 좌표를 WorkerW 클라이언트 좌표로 변환해 창을 배치한다 (해상도 변경 재배치에도 사용).</summary>
    internal static void PositionOnWorkerW(IntPtr hwnd, IntPtr workerW, int screenX, int screenY, int width, int height)
    {
        var topLeft = new POINT { X = screenX, Y = screenY };
        ScreenToClient(workerW, ref topLeft);
        SetWindowPos(hwnd, IntPtr.Zero, topLeft.X, topLeft.Y, width, height,
            SwpNoZOrder | SwpNoActivate | SwpShowWindow);
    }

    /// <summary>WorkerW에서 창을 분리한다 (창 파괴 전 호출 — 셸 계층에 잔재를 남기지 않음).</summary>
    internal static void DetachFromWorkerW(IntPtr hwnd)
    {
        if (IsWindow(hwnd))
        {
            SetParent(hwnd, IntPtr.Zero);
        }
    }

    /// <summary>원래 배경화면을 다시 그리게 한다 (앱 종료·재생 중지 시 잔상 제거 — plan D3 원상복구).</summary>
    internal static void RefreshDesktopWallpaper() =>
        SystemParametersInfoW(SpiSetDeskWallpaper, 0, null, SpifUpdateIniFile | SpifSendChange);
}
