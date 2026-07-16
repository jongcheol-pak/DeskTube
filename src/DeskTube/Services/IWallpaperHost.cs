using DeskTube.Models;

namespace DeskTube.Services;

/// <summary>
/// 배경창 수명 관리 계약 (PRD FR-2). PlaybackCoordinator(T7)는 이 인터페이스에만 의존한다
/// — 배경 표면 타입(WallpaperSurface, Win32) 접근은 실제 구현(WallpaperHost)과 플레이어 팩토리 배선에서만 (plan T5·T7 Design).
/// </summary>
public interface IWallpaperHost : IDisposable
{
    /// <summary>해당 모니터에 배경창을 생성해 WorkerW에 부착한다. 이미 있으면 재배치.</summary>
    Result Attach(MonitorInfo monitor);

    /// <summary>특정 모니터의 배경창을 해제·파괴한다. 없으면 무시.</summary>
    void Detach(string monitorId);

    /// <summary>모든 배경창을 해제하고 원래 배경화면을 복구한다.</summary>
    void DetachAll();

    /// <summary>현재 부착된 모니터 ID 목록.</summary>
    IReadOnlyCollection<string> AttachedMonitorIds { get; }

    /// <summary>
    /// WorkerW 핸들 유효성을 점검하고(Explorer 재시작 감지) 무효면 재탐색 후 전체 재부착을 시도한다.
    /// 재시도 정책(최대 3회 백오프)은 호출자(T7 주기 점검) 몫.
    /// </summary>
    Result EnsureHealthy();
}
