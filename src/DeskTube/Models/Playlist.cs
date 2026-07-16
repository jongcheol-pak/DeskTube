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
}

/// <summary>플레이리스트 — 이름 + 항목 목록. 중복 이름 허용, ID가 키 (plan T2 Edge Case).</summary>
public sealed class Playlist
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public List<PlaylistItem> Items { get; set; } = [];
}
