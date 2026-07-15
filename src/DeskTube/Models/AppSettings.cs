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

    public bool IsMuted { get; set; }

    public PlaybackMode Mode { get; set; } = PlaybackMode.Sequential;

    /// <summary>배경 재생 대상 모니터 ID 목록 (PRD FR-3). 비어 있으면 주 모니터.</summary>
    public List<string> SelectedMonitorIds { get; set; } = [];

    /// <summary>오디오 출력 모니터 ID (PRD FR-4). null이면 주 모니터.</summary>
    public string? AudioMonitorId { get; set; }

    /// <summary>화질 간접 제어 — 플레이어 렌더 세로 해상도 (PRD FR-13). 0 = 원본(스케일 없음).</summary>
    public int QualityScaleHeight { get; set; }

    /// <summary>마지막 재생 플레이리스트 (부팅 자동 재생용 — PRD FR-8).</summary>
    public Guid? LastPlaylistId { get; set; }

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
        SelectedMonitorIds ??= [];
    }
}
