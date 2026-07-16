using DeskTube.Interop;
using DeskTube.Models;

namespace DeskTube.Services;

/// <summary>
/// WorkerW 기반 배경창 수명 관리 (PRD FR-2, plan D3).
/// 배경 표면은 순수 Win32 창(WallpaperSurface) — WinUI 3 창은 크로스 프로세스 SetParent를
/// 지원하지 않아 AV로 죽는다 (plan 2026-07-16, docs/debug-2026-07-16-av-crash.md).
/// UI 스레드에서만 호출하는 전제 (plan D10 — 상태 접근 직렬화는 PlaybackCoordinator가 보장).
/// </summary>
public sealed class WallpaperHost : IWallpaperHost
{
    private sealed record Surface(WallpaperSurface Window, MonitorInfo Monitor);

    private readonly Dictionary<string, Surface> _surfaces = [];
    private IntPtr _workerW;

    public IReadOnlyCollection<string> AttachedMonitorIds => _surfaces.Keys;

    public Result Attach(MonitorInfo monitor)
    {
        var workerW = EnsureWorkerW();
        if (workerW == IntPtr.Zero)
        {
            return Result.Fail(ErrorCode.EnvironmentFailure, "배경화면 레이어(WorkerW)를 찾지 못했습니다.");
        }

        if (_surfaces.TryGetValue(monitor.Id, out var existing))
        {
            // 이미 부착됨 — 위치만 갱신 (해상도 변경 재배치 경로)
            WallpaperInterop.PositionOnWorkerW(existing.Window.Hwnd, workerW, monitor.X, monitor.Y, monitor.Width, monitor.Height);
            _surfaces[monitor.Id] = existing with { Monitor = monitor };
            return Result.Ok();
        }

        var window = new WallpaperSurface(monitor.X, monitor.Y, monitor.Width, monitor.Height);
        WallpaperInterop.AttachToWorkerW(window.Hwnd, workerW, monitor.X, monitor.Y, monitor.Width, monitor.Height);

        _surfaces[monitor.Id] = new Surface(window, monitor);
        AppLog.Write($"배경창 부착: {monitor.Id} ({monitor.Width}x{monitor.Height})");
        return Result.Ok();
    }

    public void Detach(string monitorId)
    {
        if (!_surfaces.Remove(monitorId, out var surface))
        {
            return;
        }

        CloseSurface(surface);

        if (_surfaces.Count == 0)
        {
            WallpaperInterop.RefreshDesktopWallpaper();
        }
    }

    public void DetachAll()
    {
        foreach (var surface in _surfaces.Values)
        {
            CloseSurface(surface);
        }

        _surfaces.Clear();
        WallpaperInterop.RefreshDesktopWallpaper();
    }

    /// <summary>플레이어 배선용 — 부착된 배경 표면을 반환한다 (코디네이터는 사용하지 않음, App 배선 전용 — internal 유지: Interop 타입 비공개 관례).</summary>
    internal WallpaperSurface? GetSurface(string monitorId) =>
        _surfaces.TryGetValue(monitorId, out var surface) ? surface.Window : null;

    public Result EnsureHealthy()
    {
        if (_surfaces.Count == 0)
        {
            return Result.Ok();
        }

        // Explorer 재시작 등으로 WorkerW·배경창 핸들이 무효화됐는지 점검
        var workerWAlive = _workerW != IntPtr.Zero && WallpaperInterop.IsWindow(_workerW);
        var allSurfacesAlive = _surfaces.Values.All(s => WallpaperInterop.IsWindow(s.Window.Hwnd));
        if (workerWAlive && allSurfacesAlive)
        {
            return Result.Ok();
        }

        AppLog.Write("배경화면 레이어 무효 감지 — 재부착 시도 (Explorer 재시작 가능성)");
        _workerW = IntPtr.Zero; // 재탐색 강제
        var workerW = EnsureWorkerW();
        if (workerW == IntPtr.Zero)
        {
            return Result.Fail(ErrorCode.EnvironmentFailure, "배경화면 레이어(WorkerW)를 다시 찾지 못했습니다.");
        }

        foreach (var (id, surface) in _surfaces.ToList())
        {
            if (WallpaperInterop.IsWindow(surface.Window.Hwnd))
            {
                WallpaperInterop.AttachToWorkerW(
                    surface.Window.Hwnd, workerW,
                    surface.Monitor.X, surface.Monitor.Y, surface.Monitor.Width, surface.Monitor.Height);
            }
            else
            {
                // 창 자체가 죽음 — 표면 제거 (재생성은 코디네이터가 Attach로 수행)
                _surfaces.Remove(id);
            }
        }

        return _surfaces.Count > 0
            ? Result.Ok()
            : Result.Fail(ErrorCode.EnvironmentFailure, "배경창이 모두 소실되어 재부착할 수 없습니다.");
    }

    public void Dispose() => DetachAll();

    private IntPtr EnsureWorkerW()
    {
        if (_workerW != IntPtr.Zero && WallpaperInterop.IsWindow(_workerW))
        {
            return _workerW;
        }

        _workerW = WallpaperInterop.FindWorkerW();
        return _workerW;
    }

    private static void CloseSurface(Surface surface)
    {
        // 파괴 전 셸 계층에서 분리해 잔재를 남기지 않는다 (plan D3 원상복구)
        WallpaperInterop.DetachFromWorkerW(surface.Window.Hwnd);
        surface.Window.Dispose(); // Win32 DestroyWindow — 이미 파괴된 핸들은 내부에서 무시됨
    }
}
