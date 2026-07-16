using DeskTube.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DeskTube.Views;

/// <summary>정보 페이지 (plan T6) — 서비스 의존 없음, 진입 즉시 로드.</summary>
public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required; // 전환 시 재생성 방지 (NFR-3, 내용은 정적이라 1회 로드로 충분)
        ViewModel.Load();
    }

    public AboutViewModel ViewModel { get; } = new();
}
