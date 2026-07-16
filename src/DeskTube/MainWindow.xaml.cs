using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace DeskTube;

/// <summary>
/// 진입점 Window (설정 셸). 실제 내비게이션 구성은 part2 T2에서 추가된다.
/// X 닫기는 종료가 아니라 트레이로 숨김 (PRD FR-9, plan D2).
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "DeskTube";
        AppWindow.Closing += OnAppWindowClosing;
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
