using DeskTube.Models;

namespace DeskTube.Services;

/// <summary>유튜브 IFrame 플레이어 상태 (YT.PlayerState 매핑).</summary>
public enum PlayerState
{
    Unstarted = -1,
    Ended = 0,
    Playing = 1,
    Paused = 2,
    Buffering = 3,
    Cued = 5,
}

/// <summary>
/// 플레이어 오류 코드. 양수는 유튜브 IFrame API 코드(2·5·100·101·150 — 101/150은 임베드 금지),
/// 음수는 앱 정의(-1 API 로드 실패, -2 WebView2 프로세스 실패).
/// </summary>
public readonly record struct PlayerError(int Code)
{
    /// <summary>영상 소유자가 임베드를 금지한 경우 — 다음 곡 스킵 대상 (PRD FR-1 Edge).</summary>
    public bool IsEmbedForbidden => Code is 101 or 150;
}

/// <summary>
/// 배경 플레이어 1대의 제어 계약 (PRD FR-1·5·13). PlaybackCoordinator(T7)는 이 인터페이스에만
/// 의존한다 — WebView2·UI 타입 접근은 실제 구현(PlayerHost)에만 (plan T6·T7 Design).
/// 명령은 fire-and-forget이며 결과는 이벤트로 돌아온다 (postMessage 브리지 — plan D8).
/// </summary>
public interface IPlayerHost : IDisposable
{
    /// <summary>플레이어 준비 완료 (이후 명령이 즉시 실행됨 — 이전 명령은 JS 측에서 큐잉).</summary>
    event EventHandler? Ready;

    event EventHandler<PlayerState>? StateChanged;

    event EventHandler<PlayerError>? ErrorOccurred;

    /// <summary>재생 중 1초 주기 현재 위치(초) — 다중 모니터 동기 보정용 (plan D4).</summary>
    event EventHandler<double>? TimeUpdated;

    /// <summary>WebView2 생성·player.html 로드. UI 스레드에서 호출.</summary>
    Task<Result> InitializeAsync();

    void Load(string videoId);

    void Play();

    void Pause();

    /// <summary>볼륨 0~100 (PRD FR-5).</summary>
    void SetVolume(int volume);

    /// <summary>음소거 — 오디오 대상이 아닌 모니터는 항상 true (PRD FR-4).</summary>
    void SetMuted(bool muted);

    void Seek(double seconds);

    /// <summary>화질 간접 제어 — 렌더 세로 해상도 (0 = 원본, PRD FR-13).</summary>
    void SetQualityScale(int height);

    /// <summary>동영상 크기 모드 — 채움/맞춤/늘리기 (PRD FR-16).</summary>
    void SetFitMode(FitMode mode);

    /// <summary>최근 시각 이벤트 캐시 (초). 아직 보고 없으면 0.</summary>
    double CurrentTime { get; }
}
