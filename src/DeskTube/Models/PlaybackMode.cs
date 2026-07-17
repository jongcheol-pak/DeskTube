namespace DeskTube.Models;

/// <summary>재생 모드 (PRD FR-7).</summary>
public enum PlaybackMode
{
    /// <summary>순차 — 목록 순서대로, 끝나면 처음부터 반복 (FR-7).</summary>
    Sequential,

    /// <summary>셔플 — 전곡을 무작위 순서로 1회씩 순회.</summary>
    Shuffle,

    /// <summary>랜덤 — 중복 허용 무작위 선곡.</summary>
    Random,

    /// <summary>현재 항목 반복.</summary>
    RepeatOne,

    /// <summary>전체 목록 반복 — 끝에 도달하면 처음부터 (순차와 동일 동작 — 저장 호환용 잔존).</summary>
    RepeatAll,
}
