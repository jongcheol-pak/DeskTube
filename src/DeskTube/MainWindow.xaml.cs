using DeskTube.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DeskTube;

/// <summary>
/// 진입점 Window — NavigationView 설정 셸 (plan D1). WinUIEx.WindowEx 기반 (T5):
/// 창 크기·위치 저장·복원(PersistenceId) + 최소 크기 + Mica 백드롭 + 커스텀 타이틀바.
/// X 닫기는 종료가 아니라 트레이로 숨김 (PRD FR-9, plan D2).
/// </summary>
public sealed partial class MainWindow : WinUIEx.WindowEx
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "DeskTube";

        // 창 상태 저장·복원 + 최소 크기 (T5/D4 — 패키지 앱은 WinUIEx가 ApplicationData에 자동 저장)
        PersistenceId = "MainWindow";
        MinWidth = 720;
        MinHeight = 480;

        // Mica 백드롭 + 콘텐츠 확장 타이틀바 (WinUI 표준 API — plan D4)
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

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
