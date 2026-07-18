using DeskTube.Interop;
using DeskTube.Models;
using Microsoft.UI.Dispatching;

namespace DeskTube.Services;

/// <summary>자동 일시정지 신호 종류 (NFR-1).</summary>
public enum PowerSignal
{
    /// <summary>다른 앱 전체화면 (게임·프레젠테이션 — D6).</summary>
    Fullscreen,

    /// <summary>배터리 세이버 (D7).</summary>
    BatterySaver,

    /// <summary>화면 잠금·세션 잠금 (D7).</summary>
    SessionLock,
}

/// <summary>
/// 자동 일시정지 정책 (PRD NFR-1, plan T8).
/// 신호 3종을 합성해 PauseRequested/ResumeRequested를 발행한다 — 재개는 모든 활성 신호 해제 시에만.
/// 핵심 상태 머신은 SetSignal 주입으로 테스트하고, 실신호 수집(StartMonitoring)은 분리돼 있다.
/// 이벤트는 UI 스레드에서 발행된다 (실신호는 DispatcherQueue로 마셜링, plan D10).
/// </summary>
public sealed class PowerPolicyService : IDisposable
{
    private static readonly TimeSpan FullscreenPollInterval = TimeSpan.FromSeconds(2);

    private readonly AppSettings _settings;
    private readonly HashSet<PowerSignal> _active = [];
    private bool _pauseIssued;

    // 실신호 소스 (StartMonitoring 이후에만 존재 — 테스트는 사용하지 않음)
    private DispatcherQueueTimer? _fullscreenTimer;
    private SessionInterop.SessionLockWindow? _lockWindow;

    public PowerPolicyService(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>일시정지 요청 — 활성 신호가 0→1개 이상이 될 때 1회 발행.</summary>
    public event EventHandler? PauseRequested;

    /// <summary>재개 요청 — 활성 신호가 모두 해제될 때 1회 발행 (plan T8 Edge Case: 부분 해제로는 재개 안 함).</summary>
    public event EventHandler? ResumeRequested;

    /// <summary>
    /// 신호 상태 입력 (실신호 소스와 테스트 공용 진입점).
    /// 설정에서 끈 정책의 신호는 합성에서 제외된다.
    /// </summary>
    public void SetSignal(PowerSignal signal, bool active)
    {
        var changed = active ? _active.Add(signal) : _active.Remove(signal);
        if (changed)
        {
            // 진단 로그 — 정책 경로가 무로그라 "소리 없이 멈춤"의 원인 판별이 불가능했다 (2026-07-18).
            // 신호는 변화 시에만 기록되므로 2초 폴링(전체화면)이 스팸이 되지 않는다.
            AppLog.Write($"정책 신호 변화: {signal} {(active ? "감지" : "해제")} (설정 반영 {IsEnabled(signal)})");
            Evaluate();
        }
    }

    /// <summary>설정 토글 변경 후 재평가 (part2 설정 UI가 호출 — 꺼진 정책 때문에 멈춘 상태를 해소).</summary>
    public void Reevaluate() => Evaluate();

    /// <summary>
    /// 실신호 수집 시작 (UI 스레드에서 호출) — 전체화면 2초 폴링 + 배터리 세이버 이벤트 + 세션 잠금 창.
    /// 개별 소스 초기화 실패는 로그만 남기고 나머지 신호로 계속한다 (부분 기능 저하 > 전체 실패).
    /// </summary>
    public void StartMonitoring(DispatcherQueue dispatcherQueue)
    {
        // 전체화면: 폴링 (D6 — 상태 변화 시에만 SetSignal이 이벤트로 이어짐)
        _fullscreenTimer = dispatcherQueue.CreateTimer();
        _fullscreenTimer.Interval = FullscreenPollInterval;
        _fullscreenTimer.IsRepeating = true;
        _fullscreenTimer.Tick += (_, _) => SetSignal(PowerSignal.Fullscreen, SessionInterop.IsFullscreenAppActive());
        _fullscreenTimer.Start();

        // 배터리 세이버: WinRT 이벤트 (임의 스레드 → 마셜링)
        try
        {
            Windows.System.Power.PowerManager.EnergySaverStatusChanged += (_, _) =>
                dispatcherQueue.TryEnqueue(() => SetSignal(
                    PowerSignal.BatterySaver,
                    Windows.System.Power.PowerManager.EnergySaverStatus == Windows.System.Power.EnergySaverStatus.On));

            // 시작 시점 상태 반영
            SetSignal(PowerSignal.BatterySaver,
                Windows.System.Power.PowerManager.EnergySaverStatus == Windows.System.Power.EnergySaverStatus.On);
        }
        catch (Exception ex)
        {
            AppLog.Write($"배터리 세이버 감지 시작 실패(신호 제외): {ex.GetType().Name}");
        }

        // 세션 잠금: WTS 알림 창 (WndProc 스레드 → 마셜링)
        try
        {
            _lockWindow = new SessionInterop.SessionLockWindow(locked =>
                dispatcherQueue.TryEnqueue(() => SetSignal(PowerSignal.SessionLock, locked)));
        }
        catch (InvalidOperationException ex)
        {
            AppLog.Write($"세션 잠금 감지 시작 실패(신호 제외): {ex.Message}");
        }
    }

    public void Dispose()
    {
        _fullscreenTimer?.Stop();
        _fullscreenTimer = null;
        _lockWindow?.Dispose();
        _lockWindow = null;
        // PowerManager 정적 이벤트는 해제하지 않는다 — 이 서비스는 앱 수명과 같은 싱글톤(AppServices 1회 생성).
        // AppServices가 재생성되는 구조가 생기면 구독 누적이 되므로 그때 해제 로직 필요 (part2 주의).
    }

    private bool IsEnabled(PowerSignal signal) => signal switch
    {
        PowerSignal.Fullscreen => _settings.PauseOnFullscreen,
        PowerSignal.BatterySaver => _settings.PauseOnBatterySaver,
        PowerSignal.SessionLock => _settings.PauseOnSessionLock,
        _ => false,
    };

    private void Evaluate()
    {
        var shouldPause = _active.Any(IsEnabled);
        if (shouldPause == _pauseIssued)
        {
            return; // 상태 변화 없음 — 중복 발행 방지 (멱등)
        }

        _pauseIssued = shouldPause;
        if (shouldPause)
        {
            PauseRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            ResumeRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
