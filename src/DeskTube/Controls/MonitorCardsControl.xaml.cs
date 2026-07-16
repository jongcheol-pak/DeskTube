using DeskTube.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DeskTube.Controls;

/// <summary>
/// 모니터 카드 패널 (plan T4·D4) — 홈(대형)·설정(소형) 공용 UserControl.
/// MonitorPanelViewModel의 Monitors만 바인딩하며 서비스는 직접 참조하지 않는다 (Design ③).
/// </summary>
public sealed partial class MonitorCardsControl : UserControl
{
    public MonitorCardsControl()
    {
        InitializeComponent();
        ApplyTemplateChoice();
    }

    /// <summary>카드 목록 (MonitorPanelViewModel.Monitors 바인딩용).</summary>
    public static readonly DependencyProperty MonitorsProperty = DependencyProperty.Register(
        nameof(Monitors), typeof(object), typeof(MonitorCardsControl), new PropertyMetadata(null));

    public object? Monitors
    {
        get => GetValue(MonitorsProperty);
        set => SetValue(MonitorsProperty, value);
    }

    /// <summary>대형 변형 여부 — 홈 300×188 / 설정(기본) 200×125 (시안 두 변형).</summary>
    public static readonly DependencyProperty IsLargeProperty = DependencyProperty.Register(
        nameof(IsLarge), typeof(bool), typeof(MonitorCardsControl),
        new PropertyMetadata(false, static (d, _) => ((MonitorCardsControl)d).ApplyTemplateChoice()));

    public bool IsLarge
    {
        get => (bool)GetValue(IsLargeProperty);
        set => SetValue(IsLargeProperty, value);
    }

    private void ApplyTemplateChoice() =>
        Cards.ItemTemplate = (DataTemplate)Resources[IsLarge ? "LargeCardTemplate" : "CompactCardTemplate"];

    private void OnCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MonitorChoice choice })
        {
            choice.IsSelected = !choice.IsSelected; // 토글 결과 처리(최소 1개 강제 포함)는 VM 콜백 몫
        }
    }

    // x:Bind 함수 — 선택 상태별 토큰 브러시 (다크 고정이라 Default 사전 값을 조회)
    internal static Brush CardBorder(bool selected) => Lookup(selected ? "AppAccentBrush" : "AppMonitorBorderBrush");

    internal static Brush CardBackground(bool selected) => Lookup(selected ? "AppMonitorSelectedBackgroundBrush" : "AppCardBackgroundBrush");

    internal static Brush NumberBrush(bool selected) => Lookup(selected ? "AppMonitorNumberSelectedBrush" : "AppMonitorNumberBrush");

    internal static Brush SubBrush(bool selected) => Lookup(selected ? "AppMonitorSubSelectedBrush" : "AppMonitorSubBrush");

    internal static Visibility BadgeVisibility(bool show) => show ? Visibility.Visible : Visibility.Collapsed;

    private static Brush Lookup(string key) => (Brush)Application.Current.Resources[key];
}
