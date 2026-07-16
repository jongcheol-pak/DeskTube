using DeskTube.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeskTube;

/// <summary>
/// 진입점 Window — NavigationView 설정 셸 (plan D1).
/// X 닫기는 종료가 아니라 트레이로 숨김 (PRD FR-9, plan D2).
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "DeskTube";
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

    /// <summary>트레이 상주 중이면 닫기를 취소하고 숨긴다. 트레이가 없으면(초기화 실패·종료 중) 실제 닫기.</summary>
    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (App.IsExiting || !App.IsTrayActive)
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
