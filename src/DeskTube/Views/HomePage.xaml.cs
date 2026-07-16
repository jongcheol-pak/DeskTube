using DeskTube.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace DeskTube.Views;

/// <summary>홈 페이지 — URL 입력·즉시 재생 (plan T2).</summary>
public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required; // 전환 시 재생성 방지 (NFR-3)
    }

    public HomeViewModel ViewModel { get; } = new();

    /// <summary>Enter로 바로 재생 (마우스 이동 없이).</summary>
    private void OnUrlBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.PlayCommand.CanExecute(null))
        {
            ViewModel.PlayCommand.Execute(null);
            e.Handled = true;
        }
    }
}
