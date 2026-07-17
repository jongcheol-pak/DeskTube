using DeskTube.Services;
using DeskTube.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DeskTube.Views;

/// <summary>설정 페이지 (plan T2). 서비스 준비 전 진입하면 준비 완료 시 자동 채움.</summary>
public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required; // 전환 시 재생성 방지 (NFR-3)
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public SettingsViewModel ViewModel { get; } = new();

    /// <summary>x:Bind 함수 — 볼륨 값 라벨 (시안: 슬라이더 좌측 정수 표기).</summary>
    public string FormatVolume(double volume) => ((int)volume).ToString();

    // 캐시 페이지는 Loaded/Unloaded가 진입마다 반복되므로 구독·해제를 대칭으로 유지
    // (ctor 구독 + Unloaded 해제면 2번째 진입부터 로그인 버튼이 무반응이 됨)
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

    /// <summary>로그인 창 열기 (T5) — 공통 흐름 위임 (HomePage와 공유, LoginFlow).</summary>
    private void OnSignInRequested(object? sender, EventArgs e) => LoginFlow.Open(ViewModel.Account);
}
