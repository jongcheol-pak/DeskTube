namespace DeskTube.Models;

/// <summary>앱 테마 (PRD FR-17). 기본 시스템 추종 (AGENTS 디자인 규칙 3).</summary>
public enum AppTheme
{
    /// <summary>시스템 설정 추종 — OS 테마 변경을 실시간 반영 (ElementTheme.Default).</summary>
    System,

    /// <summary>라이트 고정.</summary>
    Light,

    /// <summary>다크 고정.</summary>
    Dark,
}
