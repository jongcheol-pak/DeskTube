using System.Collections.Specialized;
using DeskTube.Models;
using DeskTube.Services;
using DeskTube.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace DeskTube.Views;

/// <summary>
/// 플레이리스트 관리 페이지 (plan T3). 다이얼로그·드래그 정렬 등 순수 뷰 상호작용만 담당하고,
/// 데이터 조작은 전부 PlaylistsViewModel에 위임한다.
/// </summary>
public sealed partial class PlaylistsPage : Page
{
    public PlaylistsPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required; // 전환 시 재생성 방지 (NFR-3)
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ViewModel.Items.CollectionChanged += OnItemsCollectionChanged;
    }

    public PlaylistsViewModel ViewModel { get; } = new();

    /// <summary>x:Bind 함수 — 선택 없음 안내의 반전 가시성.</summary>
    public Visibility InvertVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    private void OnLoaded(object sender, RoutedEventArgs e) => ViewModel.Load();

    private void OnUnloaded(object sender, RoutedEventArgs e) => ViewModel.Detach();

    /// <summary>드래그 정렬 완료 감지 — 재배치는 Remove+Add로 오므로 Add 시점에 동기화 시도.</summary>
    private async void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Move)
        {
            try
            {
                await ViewModel.SyncOrderFromViewAsync();
            }
            catch (Exception ex)
            {
                AppLog.Write($"드래그 정렬 반영 중 오류: {ex.GetType().Name} {ex.Message}");
            }
        }
    }

    private void OnUrlBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.AddItemCommand.CanExecute(null))
        {
            ViewModel.AddItemCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ---- 리스트 CRUD 다이얼로그 ----

    private async void OnNewPlaylistClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var name = await PromptNameAsync(Loc.Get("Playlists_NewDialogTitle"), string.Empty);
            if (name is not null)
            {
                await ViewModel.CreateAsync(name);
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"플레이리스트 생성 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }

    private async void OnRenameClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not PlaylistEntry entry)
        {
            return;
        }

        try
        {
            var name = await PromptNameAsync(Loc.Get("Playlists_RenameDialogTitle"), entry.Name);
            if (name is not null)
            {
                await ViewModel.RenameAsync(entry, name);
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"이름 변경 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not PlaylistEntry entry)
        {
            return;
        }

        try
        {
            // 재생 중이면 문구를 바꿔 확인 (정지 동반 삭제 — plan T3 Edge)
            var message = ViewModel.IsPlaying(entry)
                ? Loc.Get("Playlists_DeletePlayingConfirm")
                : Loc.Get("Playlists_DeleteConfirm");

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = Loc.Get("Playlists_DeleteConfirmTitle"),
                Content = message,
                PrimaryButtonText = Loc.Get("Common_Delete"),
                CloseButtonText = Loc.Get("Common_Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.DeleteAsync(entry);
            }
        }
        catch (Exception ex)
        {
            AppLog.Write($"플레이리스트 삭제 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }

    /// <summary>이름 입력 다이얼로그 — 취소하면 null.</summary>
    private async Task<string?> PromptNameAsync(string title, string initial)
    {
        var input = new TextBox
        {
            Text = initial,
            PlaceholderText = Loc.Get("Playlists_NamePlaceholder"),
        };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = input,
            PrimaryButtonText = Loc.Get("Common_Ok"),
            CloseButtonText = Loc.Get("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? input.Text : null;
    }

    // ---- 항목 행 버튼 (DataTemplate 안에서는 페이지 VM에 x:Bind가 닿지 않아 코드로 위임) ----

    private async void OnMoveUpClick(object sender, RoutedEventArgs e) => await MoveItemAsync(sender, -1);

    private async void OnMoveDownClick(object sender, RoutedEventArgs e) => await MoveItemAsync(sender, +1);

    private async Task MoveItemAsync(object sender, int delta)
    {
        if ((sender as FrameworkElement)?.DataContext is not PlaylistItemEntry item)
        {
            return;
        }

        try
        {
            await ViewModel.MoveItemAsync(item, delta);
        }
        catch (Exception ex)
        {
            AppLog.Write($"항목 이동 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }

    /// <summary>행 재생 버튼 — 해당 항목부터 재생 (FR-18, plan D3).</summary>
    private async void OnPlayItemClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not PlaylistItemEntry item)
        {
            return;
        }

        try
        {
            await ViewModel.PlayItemAsync(item);
        }
        catch (Exception ex)
        {
            AppLog.Write($"항목 재생 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }

    private async void OnRemoveItemClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not PlaylistItemEntry item)
        {
            return;
        }

        try
        {
            await ViewModel.RemoveItemAsync(item);
        }
        catch (Exception ex)
        {
            AppLog.Write($"항목 삭제 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }
}
