using Microsoft.UI.Xaml;

namespace DeskTube;

/// <summary>
/// 진입점 Window (설정 셸). 실제 내비게이션 구성은 part2 T2에서 추가된다.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "DeskTube";
    }
}
