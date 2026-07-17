using DeskTube.Services;
using DeskTube.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace DeskTube.Views;

/// <summary>홈 페이지 — URL 입력·즉시 재생 + 모니터 카드 + 빠른 재생 칩 (restyle plan T5).</summary>
public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required; // 전환 시 재생성 방지 (NFR-3)
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // 프레임 폭 = 콘텐츠 폭 + 페이지 패딩 좌우 — 손계산 파생 토큰 대신 원본 토큰에서 유도
        // (AppPagePadding·AppPageContentWidth 변경 시 자동 추종 — 다른 페이지와 폭 일치 유지)
        var contentWidth = (double)Application.Current.Resources["AppPageContentWidth"];
        var padding = (Thickness)Application.Current.Resources["AppPagePadding"];
        Layout.Width = contentWidth + padding.Left + padding.Right;
    }

    public HomeViewModel ViewModel { get; } = new();

    // 캐시 페이지는 Loaded/Unloaded가 진입마다 반복 — 구독·해제 대칭 (SettingsPage와 동일 패턴)
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Account.SignInRequested += OnSignInRequested;
        ViewModel.Load();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Detach();
        ViewModel.Account.SignInRequested -= OnSignInRequested;
    }

    /// <summary>로그인 창 열기 (FR-15) — 공통 흐름 위임 (SettingsPage와 공유, LoginFlow).</summary>
    private void OnSignInRequested(object? sender, EventArgs e) => LoginFlow.Open(ViewModel.Account);

    /// <summary>빠른 재생 칩 하단 정렬 재현 — 콘텐츠가 뷰포트보다 짧으면 뷰포트만큼 늘린다 (시안 margin-top:auto).</summary>
    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e) =>
        Layout.MinHeight = Root.ViewportHeight;

    /// <summary>Enter로 바로 재생 (마우스 이동 없이).</summary>
    private void OnUrlBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.PlayCommand.CanExecute(null))
        {
            ViewModel.PlayCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>x:Bind 함수 — 칩 재생 중 글리프의 접근성 이름·툴팁 (다국어, PlaylistsPage와 동일 키).</summary>
    public static string NowPlayingLabel() => Loc.Get("NowPlayingIndicator");

    /// <summary>칩 클릭 — 플레이리스트 페이지로 이동 + 해당 리스트 선택 (시안 D5, 순수 화면 이동이라 View 담당).</summary>
    private void OnChipClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is QuickChip chip)
        {
            ((App)Application.Current).Main?.NavigateToPlaylists(chip.Id);
        }
    }
}
