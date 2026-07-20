namespace DeskTube.Models;

/// <summary>
/// 동영상 크기 모드 (PRD FR-16). 멤버명은 CSS object-fit 동작과 일치시킨다 —
/// 기존 player.html의 .fill 클래스는 실제로 레터박스(Contain)여서 Fill 명명은 쓰지 않는다 (plan D2).
/// </summary>
public enum FitMode
{
    /// <summary>채움 — 16:9 영상으로 화면을 덮고 넘치는 부분은 잘라낸다 (Windows 배경 관례).</summary>
    Cover,

    /// <summary>맞춤 — 영상비를 유지하고 남는 부분은 검정 여백 (기본값 — AppSettings.FitMode).</summary>
    Contain,

    /// <summary>늘리기 — 영상비를 무시하고 화면에 꽉 차게 왜곡.</summary>
    Stretch,
}
