using DeskTube.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeskTube.Views;

/// <summary>설정 페이지 (plan T2). 서비스 준비 전 진입하면 준비 완료 시 자동 채움.</summary>
public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public SettingsViewModel ViewModel { get; } = new();

    private void OnLoaded(object sender, RoutedEventArgs e) => ViewModel.Load();

    private void OnUnloaded(object sender, RoutedEventArgs e) => ViewModel.Detach();
}
