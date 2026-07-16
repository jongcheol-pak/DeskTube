using Microsoft.Web.WebView2.Core;

namespace DeskTube.Services;

/// <summary>
/// 유튜브 로그인 세션 (PRD FR-15, plan T5·D4).
/// 로그인 여부 = 공유 프로필(part1 D9 단일 UDF)의 youtube.com SAPISID 쿠키 존재.
/// 확인·삭제는 화면 없는 컨트롤러를 잠시 만들었다 닫는 방식 — 플레이어·로그인 창이
/// 떠 있지 않아도 동작한다. 계정 정보는 어디에도 저장하지 않는다 (plan D9 보안).
/// </summary>
public sealed class YouTubeSessionService
{
    private readonly IntPtr _hostWindowHandle;

    /// <param name="hostWindowHandle">프로브 컨트롤러의 부모 창 (IsVisible=false — 화면 표시 없음)</param>
    public YouTubeSessionService(IntPtr hostWindowHandle)
    {
        _hostWindowHandle = hostWindowHandle;
    }

    /// <summary>로그인 세션 쿠키(SAPISID) 존재 여부 — LoginWindow의 감지 로직과 공유.</summary>
    public static bool HasSessionCookie(IEnumerable<CoreWebView2Cookie> cookies) =>
        cookies.Any(c => c.Name == "SAPISID");

    /// <summary>현재 로그인 여부 확인. 세션 만료면 false가 나와 UI에 자동 반영된다 (plan T5 Edge).</summary>
    public async Task<bool> IsSignedInAsync()
    {
        return await WithCookieManagerAsync(async manager =>
        {
            var cookies = await manager.GetCookiesAsync("https://www.youtube.com");
            return HasSessionCookie(cookies);
        });
    }

    /// <summary>로그아웃 — 공유 프로필 쿠키 전체 삭제 (확정적 공식 API — plan D4). 플레이어 리로드는 호출측 책임.</summary>
    public Task SignOutAsync()
    {
        return WithCookieManagerAsync<object?>(manager =>
        {
            manager.DeleteAllCookies();
            return Task.FromResult<object?>(null);
        });
    }

    /// <summary>화면 없는 임시 컨트롤러로 쿠키 관리자에 접근한다 (사용 후 즉시 해제 — NFR-2 메모리).</summary>
    private async Task<T> WithCookieManagerAsync<T>(Func<CoreWebView2CookieManager, Task<T>> action)
    {
        if (_hostWindowHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("세션 확인용 부모 창이 아직 없습니다.");
        }

        var environment = await WebViewEnvironment.GetAsync();
        var windowRef = CoreWebView2ControllerWindowReference.CreateFromWindowHandle((ulong)_hostWindowHandle);
        CoreWebView2Controller? controller = null;
        try
        {
            controller = await environment.CreateCoreWebView2ControllerAsync(windowRef);
            controller.IsVisible = false;
            return await action(controller.CoreWebView2.CookieManager);
        }
        finally
        {
            controller?.Close();
        }
    }
}
