using DeskTube.Services;
using DeskTube.ViewModels;
using Microsoft.UI.Xaml;
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

    /// <summary>라이선스 카드 클릭 — 해당 라이브러리 공식 사이트를 기본 브라우저로 연다 (FR-12).</summary>
    private async void OnLicenseCardClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LicenseEntry entry)
        {
            return;
        }

        try
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(entry.Url));
        }
        catch (Exception ex)
        {
            // 브라우저 열기 실패는 앱 동작에 영향 없음 — 로그만 남긴다
            AppLog.Write($"라이선스 사이트 열기 실패({entry.Id}): {ex.GetType().Name} {ex.Message}");
        }
    }
}
