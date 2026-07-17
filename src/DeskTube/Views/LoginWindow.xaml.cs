using DeskTube.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
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
        AppWindow.SetIcon("Assets/AppIcon.ico"); // 작업 표시줄 미리보기 창 아이콘 (MainWindow와 동일 — 미설정 시 기본 아이콘)
        SystemBackdrop = new MicaBackdrop(); // MainWindow와 시각 통일 (T5/D4)
        Closed += OnClosed;
        _ = InitializeAsync();
    }

    /// <summary>
    /// 창 닫힘 — WebView2를 확정 해제한다. 해제 없이 창만 닫히면 대기 중이던 네이티브 콜백
    /// (컨트롤러 생성 재시도 완료 등)이 죽은 컨트롤로 재진입해 AV로 앱 전체가 죽을 수 있다
    /// (2026-07-16 크래시 덤프 — docs/plans/2026-07-16-debug-settings-crash.md).
    /// </summary>
    private void OnClosed(object sender, WindowEventArgs args)
    {
        _isClosed = true;
        if (LoginWebView.CoreWebView2 is { } core)
        {
            core.NavigationCompleted -= OnNavigationCompleted;
        }

        LoginWebView.Close(); // 생성 진행 중이면 취소, 완료 상태면 컨트롤러 해제
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
                // WebView2 이벤트 콜스택 안에서 창을 닫으면 해제 중인 컨트롤로 네이티브 콜백이
                // 재진입할 수 있어(2026-07-16 크래시 덤프), 디스패처로 콜스택 밖에서 닫는다
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (!_isClosed)
                    {
                        Close();
                    }
                });
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"로그인 감지 확인 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }
}
