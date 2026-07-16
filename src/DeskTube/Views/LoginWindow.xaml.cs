using DeskTube.Services;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace DeskTube.Views;

/// <summary>
/// 유튜브 로그인 창 (PRD FR-15, plan T5·D4) — 공유 프로필(part1 D9)로 youtube.com 로그인 흐름을
/// 진행하고, 탐색 완료마다 세션 쿠키를 확인해 로그인이 감지되면 자동으로 닫는다.
/// 도중에 닫으면 상태 변화 없음 (재시도 가능 — plan T5 Edge).
/// </summary>
public sealed partial class LoginWindow : Window
{
    private bool _isClosed;

    public LoginWindow()
    {
        InitializeComponent();
        Title = Loc.Get("Login_Title");
        Closed += (_, _) => _isClosed = true;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var environment = await WebViewEnvironment.GetAsync();
            await LoginWebView.EnsureCoreWebView2Async(environment);
            LoginWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            LoginWebView.CoreWebView2.Navigate("https://www.youtube.com/signin");
        }
        catch (Exception ex)
        {
            AppLog.Write($"로그인 창 초기화 실패: {ex.GetType().Name} {ex.Message}");
            if (!_isClosed)
            {
                Close();
            }
        }
    }

    /// <summary>탐색 완료마다 로그인 쿠키 확인 — 감지되면 자동 닫기 (plan D4).</summary>
    private async void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        try
        {
            if (_isClosed)
            {
                return;
            }

            var cookies = await sender.CookieManager.GetCookiesAsync("https://www.youtube.com");
            if (YouTubeSessionService.HasSessionCookie(cookies) && !_isClosed)
            {
                Close();
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"로그인 감지 확인 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }
}
