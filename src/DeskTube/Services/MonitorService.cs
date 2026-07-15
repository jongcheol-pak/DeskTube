using DeskTube.Interop;

namespace DeskTube.Services;

/// <summary>모니터 1대의 정보 (UI 표시·배경창 배치용).</summary>
public sealed record MonitorInfo(
    string Id,
    string DeviceName,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary);

/// <summary>모니터 열거·변경 감지 계약 (PRD FR-3). 목킹용 인터페이스 (plan D10 DI 컨벤션).</summary>
public interface IMonitorService : IDisposable
{
    /// <summary>현재 연결된 모니터 목록 (호출 시점 열거 — 캐시 없음).</summary>
    IReadOnlyList<MonitorInfo> GetMonitors();

    /// <summary>모니터 구성 변경(연결/분리/해상도) 시 발생. WndProc 스레드에서 동기 발생 — 구독자가 마셜링.</summary>
    event EventHandler? MonitorsChanged;
}

/// <summary>
/// user32 기반 모니터 서비스. 선택·오디오 대상 해석은 순수 정적 로직으로 분리해 테스트 가능 (plan T4).
/// </summary>
public sealed class MonitorService : IMonitorService
{
    private readonly MonitorInterop.DisplayChangeWindow _changeWindow;

    public MonitorService()
    {
        _changeWindow = new MonitorInterop.DisplayChangeWindow(
            () => MonitorsChanged?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? MonitorsChanged;

    public IReadOnlyList<MonitorInfo> GetMonitors() =>
        [.. MonitorInterop.EnumerateMonitors().Select(raw => new MonitorInfo(
            CreateMonitorId(raw.DeviceName, raw.Bounds.Left, raw.Bounds.Top),
            raw.DeviceName,
            raw.Bounds.Left,
            raw.Bounds.Top,
            raw.Bounds.Right - raw.Bounds.Left,
            raw.Bounds.Bottom - raw.Bounds.Top,
            raw.IsPrimary))];

    public void Dispose() => _changeWindow.Dispose();

    /// <summary>
    /// 안정 ID 생성 — 디바이스명+위치 조합 (plan T4 Design ②).
    /// 모니터 재배치로 ID가 바뀌면 저장된 선택이 무효화되고 주 모니터로 폴백된다 (설계된 동작 — Edge Case).
    /// 해시 대신 가독 문자열을 쓴다 (디버깅·로그 판독 용이, 결정성 동일).
    /// </summary>
    public static string CreateMonitorId(string deviceName, int x, int y) => $"{deviceName}@{x},{y}";

    /// <summary>
    /// 저장된 선택 모니터 ID를 현재 모니터 목록으로 해석한다.
    /// 매칭 0개(선택 미저장·모니터 재배치·분리)면 주 모니터로 폴백한다 (PRD FR-3 기본값, plan Edge Case).
    /// 모니터가 아예 없으면 빈 목록 (재생 시작 거부는 호출자 몫 — T7).
    /// </summary>
    public static IReadOnlyList<MonitorInfo> ResolveTargets(
        IReadOnlyList<MonitorInfo> available, IReadOnlyCollection<string> selectedIds)
    {
        if (available.Count == 0)
        {
            return [];
        }

        var matched = available.Where(m => selectedIds.Contains(m.Id)).ToList();
        if (matched.Count > 0)
        {
            return matched;
        }

        var primary = available.FirstOrDefault(m => m.IsPrimary) ?? available[0];
        return [primary];
    }

    /// <summary>
    /// 오디오 출력 모니터를 결정한다 (PRD FR-4 — 대상 중 1개만 소리).
    /// 지정 ID가 재생 대상에 없으면(분리·미지정) 대상 중 주 모니터, 그것도 없으면 첫 대상으로 폴백.
    /// </summary>
    public static MonitorInfo? ResolveAudioTarget(IReadOnlyList<MonitorInfo> targets, string? audioMonitorId)
    {
        if (targets.Count == 0)
        {
            return null;
        }

        return targets.FirstOrDefault(m => m.Id == audioMonitorId)
               ?? targets.FirstOrDefault(m => m.IsPrimary)
               ?? targets[0];
    }
}
