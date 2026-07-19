namespace DeskTube.Models;

/// <summary>플레이리스트 항목 — 유튜브 URL 1건 (PRD FR-6).</summary>
public sealed class PlaylistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>사용자가 입력한 원본 URL (표시용).</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>파싱된 유튜브 영상 ID (재생용, 11자).</summary>
    public string VideoId { get; set; } = string.Empty;

    /// <summary>영상 제목 (oEmbed 조회 캐시, FR-18). 미조회·실패 시 빈 문자열 — UI는 URL로 폴백.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>채널명 (oEmbed author_name 캐시, FR-18). 미조회·실패 시 빈 문자열.</summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>재생시간(초, FR-18). 곡을 실제 재생할 때 플레이어에서 수집해 캐시한다.
    /// 0 = 아직 재생하지 않아 미수집 — UI는 공란 표시. 기존 저장 데이터에 없어도 기본값 0으로 역호환.</summary>
    public int DurationSeconds { get; set; }
}

/// <summary>플레이리스트 — 이름 + 항목 목록. 중복 이름 허용, ID가 키 (plan T2 Edge Case).</summary>
public sealed class Playlist
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public List<PlaylistItem> Items { get; set; } = [];
}
