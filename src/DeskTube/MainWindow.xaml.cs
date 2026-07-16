using DeskTube.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DeskTube;

/// <summary>
/// 진입점 Window — NavigationView 설정 셸 (plan D1). WinUIEx WindowManager 적용 (T5):
/// 창 크기·위치 저장·복원(PersistenceId) + 최소 크기 + Mica 백드롭 + 커스텀 타이틀바.
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

    public MainWindow()
    {
        InitializeComponent();
        Title = "DeskTube";

        // 창 상태 저장·복원 + 최소 크기 (T5/D4 — 패키지 앱은 WinUIEx가 ApplicationData에 자동 저장)
        _manager = WinUIEx.WindowManager.Get(this);
        _manager.PersistenceId = "MainWindow";
        _manager.MinWidth = 720;
        _manager.MinHeight = 480;

        // Mica 백드롭 + 콘텐츠 확장 타이틀바 (WinUI 표준 API — plan D4)
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // 저장 테마 적용 + 변경 재적용 등록 (FR-17 — 언어 전환 창 재생성 후에도 유지)
        Services.ThemeHelper.Register(this);

        AppWindow.Closing += OnAppWindowClosing;

        // 초기 선택 = 홈 (SelectedItem 대입이 SelectionChanged를 이미 태울 수 있어 NavigateOnce로 중복 방지)
        Nav.SelectedItem = NavHomeItem;
        NavigateOnce(typeof(HomePage));
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateOnce(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            var pageType = tag switch
            {
                "home" => typeof(HomePage),
                "playlists" => typeof(PlaylistsPage),
                "about" => typeof(AboutPage),
                _ => null,
            };
            if (pageType is not null)
            {
                NavigateOnce(pageType);
            }
        }
    }

    /// <summary>같은 페이지 재선택 시 재생성 방지.</summary>
    private void NavigateOnce(Type pageType)
    {
        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
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
    }

    /// <summary>안내 메시지 표시 (트레이 진입 안내 등 — 문구는 호출측이 리소스에서 조회).</summary>
    internal void ShowNotice(string message)
    {
        NoticeBar.Message = message;
        NoticeBar.IsOpen = true;
    }
}
