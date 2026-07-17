using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskTube.Services;

namespace DeskTube.ViewModels;

/// <summary>
/// 유튜브 계정 상태 패널 (PRD FR-15) — 설정·홈이 공유하는 공용 VM (MonitorPanelViewModel과 동형).
/// 로그인 여부 확인·문구·로그인 요청 발화·로그아웃을 담당한다. 페이지 인스턴스별로 독립이며
/// 상태 동기화는 페이지 재진입 시 Attach의 재확인으로 충분하다 (이벤트 버스 미도입 — plan D5).
/// </summary>
public partial class AccountPanelViewModel : ObservableObject
{
    private AppServices? _services;
    private YouTubeSessionService? _session;
    private bool _signedIn;

    [ObservableProperty]
    public partial string? AccountStatusText { get; set; }

    [ObservableProperty]
    public partial string? AccountButtonText { get; set; }

    /// <summary>세션 확인/전환 진행 중 — 버튼 중복 조작 방지.</summary>
    [ObservableProperty]
    public partial bool AccountActionAvailable { get; set; }

    /// <summary>로그인 창 열기 요청 — 창 생성은 View가 담당 (MVVM 경계).</summary>
    public event EventHandler? SignInRequested;

    /// <summary>
    /// 페이지 진입 시 호출 (멱등) — 최초에 세션 프로브를 만들고, 매 진입마다 상태를 재확인한다
    /// (다른 페이지에서 로그인/로그아웃한 뒤 재진입해도 최신 상태 표시 — plan Risks 수용 기준).
    /// </summary>
    public void Attach(AppServices services)
    {
        if (_services is null)
        {
            _services = services;
            _session = new YouTubeSessionService(App.MainWindowHandle);
        }

        _ = RefreshSessionAsync();
    }

    /// <summary>로그인 상태 갱신 — 페이지 진입·로그인 창 닫힘 후 호출 (세션 만료도 여기서 자동 반영).</summary>
    public async Task RefreshSessionAsync()
    {
        if (_session is null)
        {
            return;
        }

        AccountActionAvailable = false;
        AccountStatusText = Loc.Get("Settings_AccountChecking");
        try
        {
            _signedIn = await _session.IsSignedInAsync();
            AccountStatusText = Loc.Get(_signedIn ? "Settings_AccountSignedIn" : "Settings_AccountSignedOut");
            AccountButtonText = Loc.Get(_signedIn ? "Settings_SignOut" : "Settings_SignIn");
            AccountActionAvailable = true;
        }
        catch (Exception ex)
        {
            // 확인 실패(WebView2 런타임 문제 등) — 버튼 비활성으로 방어
            AppLog.Write($"로그인 상태 확인 실패: {ex.GetType().Name} {ex.Message}");
            AccountStatusText = Loc.Get("Settings_AccountUnknown");
            AccountActionAvailable = false;
        }
    }

    [RelayCommand]
    private async Task AccountActionAsync()
    {
        if (_session is null || _services is null)
        {
            return;
        }

        if (!_signedIn)
        {
            SignInRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        AccountActionAvailable = false;
        try
        {
            await _session.SignOutAsync();
            _services.Coordinator.ReloadCurrentTrack(); // 재생 중이면 비로그인 세션으로 다시 로드 (plan D4)
        }
        catch (Exception ex)
        {
            AppLog.Write($"로그아웃 실패: {ex.GetType().Name} {ex.Message}");
        }

        await RefreshSessionAsync();
    }
}
