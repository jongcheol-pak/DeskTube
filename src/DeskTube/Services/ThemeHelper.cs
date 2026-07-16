using DeskTube.Models;
using Microsoft.UI.Xaml;

namespace DeskTube.Services;

/// <summary>
/// 앱 테마 적용 헬퍼 (PRD FR-17, plan D1). 전환은 각 창 루트 Content의 RequestedTheme
/// 한 곳만 변경한다 (AGENTS 디자인 규칙 3 개정). 창 2종(MainWindow·LoginWindow)이 생성자에서
/// Register를 호출해, 언어 전환 창 재생성 후에도 테마가 자동 재적용된다
/// (새 Frame은 RequestedTheme가 초기화되므로 — 위키 desktop-localization 함정).
/// UI 스레드에서만 호출하는 전제.
/// </summary>
internal static class ThemeHelper
{
    /// <summary>테마 변경 시 재적용할 열린 창 목록 (약참조 — 닫힌 창은 SetTheme 때 지연 정리).</summary>
    private static readonly List<WeakReference<Window>> Windows = [];

    private static AppTheme _theme = AppTheme.System;

    /// <summary>시작 시 저장된 테마 반영 — 창 생성 전에 1회 호출 (첫 화면 깜빡임 방지).</summary>
    public static void Initialize(AppTheme theme) => _theme = theme;

    /// <summary>창 생성 직후 호출 — 현재 테마를 적용하고 이후 변경의 재적용 대상으로 등록.</summary>
    public static void Register(Window window)
    {
        Windows.Add(new WeakReference<Window>(window));
        Apply(window);
    }

    /// <summary>테마 변경 (설정 즉시 적용) — 열린 창 전부에 재적용. 저장은 호출측 책임.</summary>
    public static void SetTheme(AppTheme theme)
    {
        _theme = theme;
        Windows.RemoveAll(reference => !reference.TryGetTarget(out _));
        foreach (var reference in Windows)
        {
            if (reference.TryGetTarget(out var window))
            {
                Apply(window);
            }
        }
    }

    private static void Apply(Window window)
    {
        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = _theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default, // 시스템 추종 — OS 변경 실시간 반영
            };
        }
    }
}
