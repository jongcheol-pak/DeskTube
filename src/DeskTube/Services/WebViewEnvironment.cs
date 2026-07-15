using Microsoft.Web.WebView2.Core;

namespace DeskTube.Services;

/// <summary>
/// 앱 전역 단일 WebView2 환경 (plan D9).
/// - 단일 사용자 데이터 폴더 공유 → 브라우저 프로세스 공유(NFR-2 메모리) + 로그인 쿠키 공유(FR-15 전제)
/// - 자동재생 허용 인자는 환경 생성 시점에만 적용 가능 (WebView2 제약 — Investigation Log)
/// </summary>
public static class WebViewEnvironment
{
    private static Task<CoreWebView2Environment>? _environment;

    /// <summary>플레이어·로그인 창이 공유하는 환경을 반환한다 (최초 호출 시 생성).</summary>
    public static Task<CoreWebView2Environment> GetAsync() => _environment ??= CreateAsync();

    private static Task<CoreWebView2Environment> CreateAsync()
    {
        var options = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required",
        };

        var userDataFolder = Path.Combine(
            Windows.Storage.ApplicationData.Current.LocalFolder.Path, "WebView2");

        return CoreWebView2Environment.CreateWithOptionsAsync(
            browserExecutableFolder: null, userDataFolder, options).AsTask();
    }
}
