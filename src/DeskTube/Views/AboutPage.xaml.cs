using DeskTube.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DeskTube.Views;

/// <summary>정보 페이지 (plan T6) — 서비스 의존 없음, 진입 즉시 로드.</summary>
public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        ViewModel.Load();
    }

    public AboutViewModel ViewModel { get; } = new();
}
