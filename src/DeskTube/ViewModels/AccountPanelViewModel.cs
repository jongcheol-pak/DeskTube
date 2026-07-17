using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskTube.Services;

namespace DeskTube.ViewModels;

/// <summary>
/// 유튜브 계정 상태 패널 (PRD FR-15) — 설정·홈이 공유하는 공용 VM.
/// 로그인 여부 확인·문구·로그인 요청 발화·로그아웃을 담당한다.
/// 로그인 상태는 앱 전역(공유 프로필의 쿠키 세션)이므로 페이지별 복제 대신 단일 공유 인스턴스를
/// 쓴다 (Shared). 세션 프로브(숨김 WebView2 컨트롤러 생성)는 최초 1회 + 로그인 창 닫힘·로그아웃
/// 시에만 실행한다 — 페이지 진입마다 재프로브하던 방식은 기동 경로 비용·재진입 비용이 커서 변경 (2026-07-17).
/// </summary>
public partial class AccountPanelViewModel : ObservableObject
{
    /// <summary>앱 전역 공유 인스턴스 — 홈·설정이 같은 정본을 바인딩한다 (페이지 간 상태 불일치 제거).</summary>
    public static AccountPanelViewModel Shared { get; } = new();

    private AppServices? _services;
    private YouTubeSessionService? _session;
    private bool _signedIn;
    private bool _probed;

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
    /// 페이지 진입 시 호출 (멱등) — 최초 1회만 세션 프로브를 실행하고, 이후 진입은 알려진 상태로
    /// 문구만 재생성한다 (재프로브는 로그인 창 닫힘·로그아웃이 담당 — 그 외에 상태가 바뀔 경로 없음).
    /// 세션 서비스는 매번 재생성 — 언어 전환 시 창이 재생성되어 부모 창 핸들이 바뀐다 (상태 없는 경량 래퍼).
    /// </summary>
    public void Attach(AppServices services)
    {
        _services = services;
        _session = new YouTubeSessionService(App.MainWindowHandle);

        if (!_probed)
        {
            _probed = true;
            _ = RefreshSessionAsync();
            return;
        }

        ApplyTexts();
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
            ApplyTexts();
        }
        catch (Exception ex)
        {
            // 확인 실패(WebView2 런타임 문제 등) — 미로그인으로 간주하고 버튼은 로그인(=재시도 경로)으로 유지.
            // 버튼을 비활성화하면 재확인 트리거(로그인 창 닫힘)에 도달할 수 없어 일시 오류가 영구 고착된다.
            AppLog.Write($"로그인 상태 확인 실패: {ex.GetType().Name} {ex.Message}");
            _signedIn = false;
            AccountStatusText = Loc.Get("Settings_AccountUnknown");
            AccountButtonText = Loc.Get("Settings_SignIn");
            AccountActionAvailable = true;
        }
    }

    /// <summary>알려진 로그인 상태로 상태·버튼 문구를 (재)생성한다 — 재진입·언어 전환 재부착 공용.</summary>
    private void ApplyTexts()
    {
        AccountStatusText = Loc.Get(_signedIn ? "Settings_AccountSignedIn" : "Settings_AccountSignedOut");
        AccountButtonText = Loc.Get(_signedIn ? "Settings_SignOut" : "Settings_SignIn");
        AccountActionAvailable = true;
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
