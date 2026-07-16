using DeskTube.Services;
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
        ViewModel.SignInRequested += OnSignInRequested;
    }

    public SettingsViewModel ViewModel { get; } = new();

    private void OnLoaded(object sender, RoutedEventArgs e) => ViewModel.Load();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Detach();
        ViewModel.SignInRequested -= OnSignInRequested;
    }

    /// <summary>로그인 창 열기 (T5) — 닫히면 세션 상태 재확인 (도중 닫기 = 상태 변화 없음).</summary>
    private void OnSignInRequested(object? sender, EventArgs e)
    {
        var login = new LoginWindow();
        login.Closed += async (_, _) =>
        {
            try
            {
                await ViewModel.RefreshSessionAsync();
            }
            catch (Exception ex)
            {
                AppLog.Write($"로그인 후 상태 갱신 실패: {ex.GetType().Name} {ex.Message}");
            }
        };
        login.Activate();
    }
}
