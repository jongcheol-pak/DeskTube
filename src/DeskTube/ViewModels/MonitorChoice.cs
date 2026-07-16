using CommunityToolkit.Mvvm.ComponentModel;

namespace DeskTube.ViewModels;

/// <summary>
/// 모니터 카드 1장 (재생 대상 다중 선택 — plan T4, 홈·설정 공용).
/// SettingsViewModel 내부 클래스에서 분리·확장 (시안 카드 표시 속성 추가).
/// </summary>
public partial class MonitorChoice : ObservableObject
{
    private readonly Action<MonitorChoice> _selectionChanged;

    public MonitorChoice(
        string id,
        int number,
        string displayName,
        string resolutionLabel,
        bool isPrimary,
        bool isSelected,
        Action<MonitorChoice> selectionChanged)
    {
        Id = id;
        Number = number;
        DisplayName = displayName;
        ResolutionLabel = resolutionLabel;
        IsPrimary = isPrimary;
        _selectionChanged = selectionChanged;

        // 초기값 대입이 변경 콜백을 타지 않도록 억제 (partial property는 초기화 식 불가)
        SuppressCallback = true;
        IsSelected = isSelected;
        SuppressCallback = false;
    }

    public string Id { get; }

    /// <summary>카드 중앙 표시 번호 (1-based — 시안).</summary>
    public int Number { get; }

    /// <summary>오디오 콤보·접근성 이름용 전체 표기 ("디스플레이 1 — 2560×1600").</summary>
    public string DisplayName { get; }

    /// <summary>카드 좌하단 서브라벨 ("2560×1600 · 주 모니터" — 시안).</summary>
    public string ResolutionLabel { get; }

    public bool IsPrimary { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>"🔊 소리" 배지 표시 — 선택됨 ∧ 오디오 대상 ∧ 비음소거 (시안 식).</summary>
    [ObservableProperty]
    public partial bool ShowAudioBadge { get; set; }

    /// <summary>마지막 체크 해제 차단의 되돌림 중 — 콜백 재진입 억제.</summary>
    internal bool SuppressCallback { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (!SuppressCallback)
        {
            _selectionChanged(this);
        }
    }
}
