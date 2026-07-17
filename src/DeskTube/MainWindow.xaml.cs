using DeskTube.Services;
using DeskTube.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DeskTube;

/// <summary>
/// 진입점 Window — NavigationView 설정 셸 (plan T3, 시안 DeskTube 1a). WinUIEx WindowManager 적용:
/// 창 크기·위치 저장·복원(PersistenceId) + 최소 크기 + 커스텀 타이틀바(44px, 시스템 캡션 버튼).
/// 배경은 불투명 토큰(시안 — Mica 제거, plan D10), 테마는 App.xaml 다크 고정.
/// WindowEx 상속 대신 WindowManager를 쓴 이유 — XAML 루트를 WindowEx로 바꾸면
/// 생성되는 XamlTypeInfo.g.cs가 obsolete 속성(Icon)을 등록해 CS0618 경고 발생
/// (이 프로젝트 빌드로 재현 확인, 생성 코드라 억제 불가). WindowManager는 동일 기능을
/// 임의 창에 제공한다 (WinUIEx 공식 문서 WindowManager.md).
/// X 닫기는 종료가 아니라 트레이로 숨김 (PRD FR-9, plan D2).
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>창 상태 관리자 — 수명 보장이 문서화돼 있지 않아 방어적으로 창 수명 동안 참조를 유지.</summary>
    private readonly WinUIEx.WindowManager _manager;

    /// <summary>토스트 자동 소멸 타이머 (toast plan D2 — 성공/정보 3초, 오류/경고 5초).</summary>
    private readonly DispatcherTimer _toastTimer = new();

    public MainWindow()
    {
        InitializeComponent();

        // 공용 토스트 호스트 등록 — 언어 전환 창 재생성 시 새 인스턴스가 덮어쓴다 (toast plan T1)
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            ToastHost.Visibility = Visibility.Collapsed;
        };
        ToastService.Attach(PresentToast, DispatcherQueue);

        // 앱 이름 — 언어별 표기(ko 데스크튜브). 언어 전환은 창 재생성(App.ApplyLanguageChange)이 재배정한다.
        Title = Loc.Get("AppDisplayName");
        AppTitleText.Text = Loc.Get("AppDisplayName");

        // 창 아이콘 — 작업 표시줄 미리보기(썸네일 플라이아웃)는 패키지가 아니라 창 아이콘을 쓰는데,
        // WinUI 3 Window는 이를 자동 설정하지 않아 기본 아이콘이 보인다 (경로는 실행 폴더 기준 Content)
        AppWindow.SetIcon("Assets/AppIcon.ico");

        // 창 상태 저장·복원 + 최소 크기 (T5/D4 — 패키지 앱은 WinUIEx가 ApplicationData에 자동 저장)
        _manager = WinUIEx.WindowManager.Get(this);
        _manager.PersistenceId = "MainWindow";
        _manager.MinWidth = 720;
        _manager.MinHeight = 480;

        // 콘텐츠 확장 타이틀바 — 캡션 버튼은 시스템 유지(스냅 레이아웃·접근성), 색만 토큰 정합 (Q3)
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ApplyCaptionButtonColors();

        AppWindow.Closing += OnAppWindowClosing;

        // 초기 선택 = 홈 (SelectedItem 대입이 SelectionChanged를 이미 태울 수 있어 NavigateOnce로 중복 방지)
        Nav.SelectedItem = NavHomeItem;
        NavigateOnce(typeof(HomePage));
    }

    /// <summary>
    /// 시스템 캡션 버튼을 다크 셸 토큰과 정합 — 배경 투명 + 전경/hover를 토큰 색으로.
    /// (캡션 버튼은 앱 테마가 아니라 시스템 테마를 따를 수 있어 명시 지정 — 시각은 HUMAN-VERIFY)
    /// </summary>
    private void ApplyCaptionButtonColors()
    {
        var titleBar = AppWindow.TitleBar;
        var transparent = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonBackgroundColor = transparent;
        titleBar.ButtonInactiveBackgroundColor = transparent;
        titleBar.ButtonForegroundColor = GetTokenColor("AppTextTertiaryColor");
        titleBar.ButtonInactiveForegroundColor = GetTokenColor("AppTextTertiaryColor");
        titleBar.ButtonHoverBackgroundColor = GetTokenColor("AppActiveBackgroundColor");
        titleBar.ButtonHoverForegroundColor = GetTokenColor("AppTextPrimaryColor");
    }

    /// <summary>DesignTokens의 Color 토큰 조회 (없으면 안전 폴백 — 시작을 막지 않는다).</summary>
    private static Windows.UI.Color GetTokenColor(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is Windows.UI.Color color
            ? color
            : Microsoft.UI.Colors.Gray;

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            var pageType = tag switch
            {
                "home" => typeof(HomePage),
                "playlists" => typeof(PlaylistsPage),
                "settings" => typeof(SettingsPage),
                "about" => typeof(AboutPage),
                _ => null,
            };
            if (pageType is not null)
            {
                NavigateOnce(pageType);
            }
        }
    }

    /// <summary>다음 NavigateOnce에 전달할 페이지 파라미터 (칩 이동 등 — 소비 후 즉시 비움).</summary>
    private object? _pendingNavParameter;

    /// <summary>
    /// 플레이리스트 페이지로 이동 + 해당 리스트 선택 (홈 빠른 재생 칩 진입점 — plan T3·D5).
    /// 이미 플레이리스트 페이지면 선택만 갱신되도록 파라미터를 실어 재탐색한다.
    /// </summary>
    internal void NavigateToPlaylists(Guid playlistId)
    {
        _pendingNavParameter = playlistId;
        if (!ReferenceEquals(Nav.SelectedItem, NavPlaylistsItem))
        {
            Nav.SelectedItem = NavPlaylistsItem; // SelectionChanged → NavigateOnce가 파라미터 소비
        }
        else
        {
            NavigateOnce(typeof(PlaylistsPage));
        }
    }

    /// <summary>같은 페이지 재선택 시 재생성 방지. 대기 파라미터가 있으면 같은 페이지여도 전달한다.</summary>
    private void NavigateOnce(Type pageType)
    {
        var parameter = _pendingNavParameter;
        _pendingNavParameter = null;
        if (ContentFrame.CurrentSourcePageType != pageType || parameter is not null)
        {
            ContentFrame.Navigate(pageType, parameter);
        }
    }

    /// <summary>닫기→숨김 우회 플래그 (언어 전환 시 창 교체 — plan T7).</summary>
    private bool _allowRealClose;

    /// <summary>닫기→숨김 로직을 우회하고 실제로 닫는다 (언어 전환 창 교체 전용).</summary>
    internal void ForceClose()
    {
        _allowRealClose = true;
        Close();
    }

    /// <summary>트레이 상주 중이면 닫기를 취소하고 숨긴다. 트레이가 없으면(초기화 실패·종료 중) 실제 닫기.</summary>
    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowRealClose || App.IsExiting || !App.IsTrayActive)
        {
            return;
        }

        args.Cancel = true;
        sender.Hide();

        // 창 숨김 = 유휴 진입 — 정지(Stopped) 상태일 때만 워킹셋 반환 (NFR-2, plan D6 —
        // 재생은 물론 일시정지 중에도 트림하지 않음: 재개가 페이지 폴트로 느려질 수 있음)
        if (App.Services is null || App.Services.Coordinator.Status == Services.PlaybackStatus.Stopped)
        {
            Interop.ProcessInterop.TrimWorkingSet();
        }
    }

    /// <summary>
    /// 공용 토스트 표시 (toast plan T1) — 연속 알림은 최신 메시지로 교체 + 타이머 리셋 (D3).
    /// 글리프: 성공 E73E 체크 / 경고 E7BA / 오류 E783 / 정보 E946 (소스에는 PUA 리터럴로 저장).
    /// </summary>
    private void PresentToast(string message, InfoBarSeverity severity)
    {
        ToastText.Text = message;
        ToastIcon.Glyph = severity switch
        {
            InfoBarSeverity.Success => "",
            InfoBarSeverity.Warning => "",
            InfoBarSeverity.Error => "",
            _ => "",
        };
        // 아이콘 색 — 성공은 코럴, 정보는 보조 텍스트, 경고/오류는 밝은 코럴 (D5 — 토큰만 사용)
        ToastIcon.Foreground = (Brush)Application.Current.Resources[severity switch
        {
            InfoBarSeverity.Success => "AppAccentBrush",
            InfoBarSeverity.Informational => "AppTextSecondaryBrush",
            _ => "AppAccentHoverBrush",
        }];

        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromSeconds(
            severity is InfoBarSeverity.Error or InfoBarSeverity.Warning ? 5 : 3);
        ToastHost.Visibility = Visibility.Visible;
        _toastTimer.Start();
    }
}
