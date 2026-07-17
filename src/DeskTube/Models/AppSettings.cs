namespace DeskTube.Models;

/// <summary>
/// 앱 설정 — settings.json으로 영속화 (PRD FR-5·FR-14, plan D5).
/// 새 필드는 기본값과 함께 추가한다 (SchemaVersion으로 하위 호환 예약).
/// </summary>
public sealed class AppSettings
{
    /// <summary>직렬화 형식 버전 (향후 마이그레이션 예약 — plan D5).</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>볼륨 0~100 (PRD FR-5).</summary>
    public int Volume { get; set; } = 50;

    /// <summary>음소거 (PRD FR-5 — 기본 켬, 2026-07-17).</summary>
    public bool IsMuted { get; set; } = true;

    public PlaybackMode Mode { get; set; } = PlaybackMode.Sequential;

    /// <summary>배경 재생 대상 모니터 ID 목록 (PRD FR-3). 비어 있으면 주 모니터.</summary>
    public List<string> SelectedMonitorIds { get; set; } = [];

    /// <summary>오디오 출력 모니터 ID (PRD FR-4). null이면 주 모니터.</summary>
    public string? AudioMonitorId { get; set; }

    /// <summary>화질 간접 제어 — 플레이어 렌더 세로 해상도 (PRD FR-13). 0 = 원본(스케일 없음).</summary>
    public int QualityScaleHeight { get; set; }

    /// <summary>동영상 크기 모드 (PRD FR-16). 기본 맞춤(Contain — 2026-07-17 채움에서 변경).</summary>
    public FitMode FitMode { get; set; } = FitMode.Contain;

    /// <summary>자막 표시 (PRD FR-20 — 기본 끔). 켜면 강제 표시, 끄면 계정 선호와 무관하게 숨김.</summary>
    public bool CaptionsEnabled { get; set; }

    /// <summary>미러 모니터 화질 하향 (NFR-2 부하 절감 — 기본 꺼짐, 사용자 opt-in).</summary>
    public bool ReduceMirrorQuality { get; set; }

    /// <summary>마지막 재생 플레이리스트 (부팅 자동 재생용 — PRD FR-8).</summary>
    public Guid? LastPlaylistId { get; set; }

    /// <summary>마지막 재생 항목 — 앱 시작·부팅 자동 재생의 항목 단위 재개용 (PRD FR-19·FR-8).</summary>
    public Guid? LastItemId { get; set; }

    /// <summary>앱 시작 후 자동 재생 (PRD FR-19 — 기본 켜짐, 2026-07-17). 켜면 일반 실행 시 마지막 항목부터 재생.</summary>
    public bool AutoPlayOnLaunch { get; set; } = true;

    /// <summary>플레이리스트 화면에서 마지막에 선택한 리스트 (재생 이력 LastPlaylistId와 별개 — FR-18 기본 선택용).</summary>
    public Guid? LastSelectedPlaylistId { get; set; }

    /// <summary>홈에서 마지막으로 재생한 URL — 재실행 시 입력란 복원 표시용 (PRD FR-1, 재생 성공 시에만 기록).</summary>
    public string? LastHomeUrl { get; set; }

    /// <summary>홈 즉시 재생("빠른 재생") 플레이리스트의 안정 식별자 — 자동 재생 제외 판정용 (FR-8).
    /// 표시 이름은 언어 전환·동명 사용자 리스트와 충돌하므로 식별에 쓰지 않는다 (2026-07-17 수정).</summary>
    public Guid? QuickPlaylistId { get; set; }

    /// <summary>자동 일시정지 정책 (PRD NFR-1 — 기본 모두 켬).</summary>
    public bool PauseOnFullscreen { get; set; } = true;

    public bool PauseOnBatterySaver { get; set; } = true;

    public bool PauseOnSessionLock { get; set; } = true;

    /// <summary>UI 언어 코드 (null = 시스템 추종 — part2 T7).</summary>
    public string? Language { get; set; }

    /// <summary>로드 직후 범위를 벗어난 값을 안전 범위로 보정한다 (손상·수동 편집 대비).</summary>
    public void Normalize()
    {
        Volume = Math.Clamp(Volume, 0, 100);
        if (QualityScaleHeight < 0)
        {
            QualityScaleHeight = 0;
        }
        if (!Enum.IsDefined(FitMode))
        {
            FitMode = FitMode.Contain; // 손상 값 폴백은 기본값과 동일하게 (FR-16)
        }
        SelectedMonitorIds ??= [];
    }
}
