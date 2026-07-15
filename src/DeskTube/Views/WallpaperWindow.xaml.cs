using Microsoft.UI.Xaml;

namespace DeskTube.Views;

/// <summary>
/// 모니터 1대를 덮는 배경창 — WorkerW의 자식으로 부착된다 (WallpaperHost가 수명 관리).
/// 콘텐츠(T6 플레이어 WebView2)는 AttachContent로 받는다.
/// </summary>
public sealed partial class WallpaperWindow : Window
{
    public WallpaperWindow()
    {
        InitializeComponent();
    }

    /// <summary>플레이어 등 콘텐츠를 창에 채운다 (기존 콘텐츠는 교체).</summary>
    public void AttachContent(UIElement content)
    {
        RootGrid.Children.Clear();
        RootGrid.Children.Add(content);
    }
}
