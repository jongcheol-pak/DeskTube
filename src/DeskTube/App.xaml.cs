using DeskTube.Models;
using DeskTube.Services;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.WindowsAppRuntime;

namespace DeskTube;

/// <summary>
/// 앱 진입점. OnLaunched에서 MainWindow를 생성한다 (WinUI 3 Desktop — Window.Current 사용 금지).
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private TrayIconService? _tray;

    public App()
    {
        // 배포 초기화를 XAML 초기화(InitializeComponent)보다 먼저 수행 — 아래 메서드 주석 참조
        InitializeWindowsAppRuntime();

        // 저장된 언어를 XAML 초기화 전에 동기 선적용 — x:Uid 정적 문구의 언어는
        // 요소 생성 시점에 고정된다 (plan T7, AGENTS 다국어 규칙 3)
        ApplySavedStartupOverrides();

        InitializeComponent();

        // 처리되지 않은 예외 로깅 — 핸들러가 없으면 프로세스가 아무 기록 없이 죽어 크래시
        // 원인 추적이 불가능하다 (2026-07-16 조사 — docs/plans/2026-07-16-debug-settings-crash.md).
        // 기록만 하고 예외는 삼키지 않는다 (Handled 미설정 — 상태 불명인 채 계속 실행 방지).
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    /// <summary>XAML 디스패처 경로의 미처리 예외 기록 (이벤트 핸들러·바인딩 예외 등).</summary>
    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        AppLog.Write($"처리되지 않은 XAML 예외: {e.Exception?.GetType().Name} {e.Message}\n{e.Exception?.StackTrace}");
    }

    /// <summary>XAML 경로 밖(스레드 풀·WinRT 콜백 등)의 미처리 예외 기록 — 종료 직전 마지막 단서.</summary>
    private static void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        AppLog.Write($"처리되지 않은 도메인 예외: {e.ExceptionObject}");
    }

    /// <summary>settings.json의 Language만 동기로 선읽기해 적용한다 (없으면 시스템 언어).</summary>
    private static void ApplySavedStartupOverrides()
    {
        try
        {
            var path = Path.Combine(
                Windows.Storage.ApplicationData.Current.LocalFolder.Path, "settings.json");
            if (!File.Exists(path))
            {
                return;
            }

            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("Language", out var language) &&
                language.ValueKind == System.Text.Json.JsonValueKind.String &&
                !string.IsNullOrEmpty(language.GetString()))
            {
                Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = language.GetString();
            }
        }
        catch (Exception)
        {
            // 파손·접근 실패 시 시스템 언어 폴백 (AppLog는 OnLaunched에서 초기화되므로 여기선 기록 생략)
        }
    }

    /// <summary>서비스 그래프 (컴포지션 루트 — OnLaunched에서 비동기 초기화, UI는 null 확인 후 사용).</summary>
    public static AppServices? Services { get; private set; }

    /// <summary>트레이 상주 여부 — 창 닫기를 숨김으로 바꿀지 판단 (MainWindow가 사용, plan T1).</summary>
    public static bool IsTrayActive => Current is App app && app._tray is not null;

    /// <summary>설정 셸 창 접근자 — 페이지의 창 내 이동 진입점 (홈 칩 → 플레이리스트, plan T3).</summary>
    internal MainWindow? Main => _window as MainWindow;

    /// <summary>트레이 '종료' 진행 중 — 창 닫기 취소(숨김) 로직을 우회한다.</summary>
    internal static bool IsExiting { get; private set; }

    /// <summary>
    /// Services 준비 완료 알림 (UI 스레드에서 발생) — 창이 서비스보다 먼저 뜨므로,
    /// 준비 전에 열린 페이지가 이 이벤트로 늦은 초기화를 반영한다 (part2 T2).
    /// </summary>
    public static event EventHandler? ServicesInitialized;

    /// <summary>세션 프로브 컨트롤러의 부모 창 핸들 (T5 — 화면 표시 없음, 창 미생성 시 Zero).</summary>
    internal static IntPtr MainWindowHandle =>
        Current is App { _window: not null } app
            ? WinRT.Interop.WindowNative.GetWindowHandle(app._window)
            : IntPtr.Zero;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppLog.Initialize(Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "logs"));
        // 시작·종료 마커 — 이전 세션이 "앱 종료" 로그 없이 끝났으면 비정상 종료(크래시·강제 종료)로
        // 추정한다. 단 OS 종료·로그오프는 WM_ENDSESSION 후 즉시 종료돼 마커가 안 남을 수 있으므로
        // 단정하지 않는다 (2026-07-18 조사). ProcessExit 마커는 정상 프로세스 종료(트레이 종료 포함)
        // 커버리지를 넓히는 최선 노력 경로 — 판별 오탐(정상 종료를 크래시로 오판)을 줄인다.
        AppDomain.CurrentDomain.ProcessExit += static (_, _) => AppLog.Write("=== 앱 종료 (프로세스 종료) ===");
        AppLog.Write("=== 앱 시작 ===");

        // 단일 인스턴스 게이트(Program)의 폴백 사유 지연 기록 — 게이트 시점엔 AppLog 미초기화 (FR-22 D4)
        if (Program.SingleInstanceFallbackReason is { } gateFallback)
        {
            AppLog.Write(gateFallback);
        }

        // 자동 시작(부팅) 판별 — 활성화 종류 우선, 조회 실패 시 명령줄 인자 폴백 (plan T4·D3)
        var quietStart = StartupService.WasActivatedByStartupTask()
            || StartupArgs.HasStartupFlag(Environment.GetCommandLineArgs());

        _window = new MainWindow();
        if (!quietStart)
        {
            _window.Activate(); // 자동 시작이면 창 없이 트레이만 (PRD FR-8)
        }

        // async void 회피 — 실패는 로그로 남기고 앱은 뜬다 (UI가 Services null로 미준비 안내)
        _ = InitializeServicesAsync(quietStart);
    }

    private async Task InitializeServicesAsync(bool autoPlay)
    {
        try
        {
            Services = await AppServices.CreateAsync(_window!.DispatcherQueue);
        }
        catch (Exception ex)
        {
            AppLog.Write($"서비스 초기화 실패: {ex.GetType().Name} {ex.Message}");
            return; // 트레이 미생성 — 이 경우 창 닫기는 실제 종료로 동작한다 (MainWindow 참조)
        }

        // await 후에도 UI 스레드 컨텍스트 (WinUI SynchronizationContext) — 트레이는 UI 스레드에서 생성
        _tray = new TrayIconService(Services, ShowMainWindow, ExitApplication);
        _tray.Initialize();

        ServicesInitialized?.Invoke(this, EventArgs.Empty);

        // 전 항목 재생 불가 정지 안내 — 창이 숨겨진(트레이 전용) 상태에서도 보이도록 창 표시 경로로 라우팅.
        // 코디네이터는 표시 수단을 모른다 (이벤트만 발화 — 발생 스레드 비보장이라 UI로 마셜링).
        Services.Coordinator.AllItemsFailed += OnAllItemsFailed;

        // FR-19 토글은 일반 실행에만 의미가 있고, 부팅 자동 시작(autoPlay)은 FR-8로 항상 재생한다
        if (autoPlay || Services.Settings.AutoPlayOnLaunch)
        {
            await TryAutoPlayLastAsync();
        }
    }

    private void OnAllItemsFailed(object? sender, EventArgs e) =>
        _window?.DispatcherQueue.TryEnqueue(() => ShowMainWindow(Loc.Get("Playback_AllItemsFailed")));

    /// <summary>자동 시작·앱 시작 자동 재생 (PRD FR-8·FR-19) — 마지막 재생 항목부터 조용히 재생 시작. 리스트가 없으면 생략하고 트레이만 상주 (plan T4 Edge).</summary>
    private async Task TryAutoPlayLastAsync()
    {
        var services = Services!;

        // 마지막 재생이 홈 즉시 재생("빠른 재생")이면 자동 재생 생략 (FR-8·FR-19 예외 — 일반 실행·부팅 공통).
        // 식별은 안정 ID(Settings.QuickPlaylistId) — 언어 전환·동명 사용자 리스트에 흔들리지 않는다 (2026-07-17 수정).
        // ID 미기록(구버전 데이터)이면 이름 폴백으로 판정한다 (홈 재생 1회 후 ID가 기록되며 폴백은 소멸).
        if (services.Settings.LastPlaylistId is { } lastId)
        {
            var isQuickPlay = services.Settings.QuickPlaylistId is { } quickId
                ? lastId == quickId
                : services.Library.Playlists.FirstOrDefault(p => p.Id == lastId)?.Name
                    == Loc.Get("Home_QuickPlaylistName");
            if (isQuickPlay)
            {
                AppLog.Write("자동 시작: 마지막 재생이 홈 즉시 재생이라 자동 재생을 생략합니다.");
                return;
            }
        }

        // 마지막 리스트 재개는 트레이 재생과 공용 경로 (StartLastAsync — 중복 구현 제거, 2026-07-17)
        var result = await services.Coordinator.StartLastAsync();
        if (!result.IsSuccess)
        {
            AppLog.Write(result.Code == ErrorCode.NotFound
                ? "자동 시작: 재생할 마지막 플레이리스트가 없어 재생을 생략합니다."
                : $"자동 시작 재생 실패: {result.Message}");
        }
    }

    /// <summary>설정 창 표시 — 트레이 메뉴·더블클릭 진입점 (notice가 있으면 토스트로 안내 — toast plan T1).</summary>
    internal void ShowMainWindow(string? notice)
    {
        if (_window is not MainWindow window)
        {
            return;
        }

        window.AppWindow.Show();
        window.Activate();
        if (notice is not null)
        {
            ToastService.Show(notice); // 공용 토스트 (toast plan T1 — 창 표시 후 안내)
        }
    }

    /// <summary>
    /// 언어 변경 적용 (plan T7, AGENTS 다국어 규칙 3) — 리소스 캐시 초기화 후 트레이 메뉴와
    /// 설정 셸(창)을 새로 만든다. x:Uid·Loc 문구는 요소 생성 시점에 고정되므로 재생성이 필요하다.
    /// (테마는 Application 수준 다크 고정이라 창 재생성과 무관하게 유지된다.)
    /// </summary>
    internal void ApplyLanguageChange()
    {
        Loc.Reset();

        if (Services is not null && _tray is not null)
        {
            _tray.Dispose();
            _tray = new TrayIconService(Services, ShowMainWindow, ExitApplication);
            _tray.Initialize();
        }

        var old = _window as MainWindow;
        _window = new MainWindow();
        _window.Activate();
        old?.ForceClose(); // 닫기→숨김 로직 우회 실제 닫기 (교체 완료 후)
    }

    /// <summary>트레이 '종료' — 재생 정리·배경 복구(Services.Dispose) 후 앱 종료 (plan T1 Edge).</summary>
    internal void ExitApplication()
    {
        AppLog.Write("=== 앱 종료 (트레이 종료 선택) ===");
        IsExiting = true;
        _tray?.Dispose();
        _tray = null;
        Services?.Dispose();
        Services = null;
        Exit();
    }

    /// <summary>
    /// Windows App Runtime 배포 초기화 (실패 처리·옵션은 SDK 원본 AutoInitializer와 동일:
    /// OnErrorShowUI + 실패 시 HResult로 종료).
    /// 단 실행 시점은 원본(module initializer — 어셈블리 타입 최초 접근 전)보다 늦은 App 생성자다
    /// — 테스트 호스트가 앱 어셈블리를 로드할 때 초기화가 실행돼 죽는 문제(0x80040154) 때문에
    /// csproj에서 자동 초기화를 끄고 여기서 호출한다. 프레임워크 패키지의 최초 설치/복구
    /// 시나리오(클린 설치 후 첫 실행)는 실기 HUMAN-VERIFY 대상 (plan Verification Strategy).
    /// </summary>
    private static void InitializeWindowsAppRuntime()
    {
        var result = DeploymentManager.Initialize(new DeploymentInitializeOptions { OnErrorShowUI = true });
        if (result.Status != DeploymentStatus.Ok)
        {
            var hr = result.ExtendedError.HResult;
            Environment.Exit(hr);
        }
    }
}
