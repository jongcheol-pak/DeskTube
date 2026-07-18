using System.Collections.Specialized;
using DeskTube.Models;
using DeskTube.Services;
using DeskTube.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
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

    /// <summary>홈 빠른 재생 칩 진입 — 파라미터의 리스트를 선택 (restyle T5·D5, MainWindow.NavigateToPlaylists).</summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Guid playlistId)
        {
            ViewModel.SelectPlaylist(playlistId);
        }
    }

    /// <summary>
    /// 가로 스크롤 래퍼는 콘텐츠를 무한 폭으로 측정한다 — MaxWidth를 뷰포트 폭(최소 900)으로 걸어
    /// 긴 영상 제목이 페이지 폭을 무한정 키우는 대신 말줄임(CharacterEllipsis)되게 한다.
    /// </summary>
    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e) =>
        Layout.MaxWidth = Math.Max(
            Root.ViewportWidth, (double)Application.Current.Resources["AppPageContentWidth"]);

    /// <summary>x:Bind 함수 — 선택 없음 안내의 반전 가시성.</summary>
    public Visibility InvertVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>x:Bind 함수 — 곡수 표기 ("12곡", 시안 헤더 행).</summary>
    public string FormatCount(int count) => string.Format(Loc.Get("Playlists_CountFormat"), count);

    /// <summary>x:Bind 함수 — 활성 리스트 항목의 텍스트 강조 (시안 #FFF w600 / 비활성 #B8B8BE).</summary>
    public static Brush ActiveNameBrush(bool isActive) =>
        TokenBrush(isActive ? "AppTextStrongBrush" : "AppTextNavBrush");

    public static Windows.UI.Text.FontWeight ActiveWeight(bool isActive) =>
        isActive ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

    /// <summary>x:Bind 함수 — 재생 중 글리프의 접근성 이름·툴팁 (다국어, MonitorCardsControl BadgeToggleName 선례).</summary>
    public static string NowPlayingLabel() => Loc.Get("NowPlayingIndicator");

    /// <summary>x:Bind 함수 — 재생 중이면 순위 번호를 숨긴다(같은 자리 스피커 글리프가 대신 표시 — now-playing item plan D3).</summary>
    public static Visibility RankVisibility(bool isNowPlaying) =>
        isNowPlaying ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>x:Bind 함수 — 듣기 버튼 트레일링 정지 아이콘의 접근성 이름·툴팁 (Tray_Stop 재사용 — mode-indicator plan D6).</summary>
    public static string StopLabel() => Loc.Get("Tray_Stop");

    /// <summary>x:Bind 함수 — 행 재생/정지 토글 글리프 (재생 중인 곡 행만 정지 — stop-toggle plan D2).</summary>
    public static string RowPlayGlyph(bool isNowPlaying) => isNowPlaying ? "\uE71A" : "\uE768";

    /// <summary>x:Bind 함수 — 행 재생/정지 버튼의 상태 반영 접근성 이름 (NowPlayingLabel과 동일 Loc 패턴).</summary>
    public static string RowPlayName(bool isNowPlaying) =>
        Loc.Get(isNowPlaying ? "Tray_Stop" : "ItemPlayName");

    // ---- hover 코럴 처리 (시안 — x:Bind 정적 색은 hover 상태를 모르므로 포인터 이벤트로 교체) ----
    // PointerEntered/Exited는 IsEnabled=False에도 발생하므로 비활성 버튼 강조 방지 가드 필수 (리뷰 M1)

    private static Brush TokenBrush(string key) => (Brush)Application.Current.Resources[key];

    /// <summary>외곽선 버튼(추가·셔플듣기) hover — 테두리만 코럴.</summary>
    private void OnOutlineButtonPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button { IsEnabled: true } button)
        {
            button.BorderBrush = TokenBrush("AppAccentBrush");
        }
    }

    private void OnOutlineButtonPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.BorderBrush = TokenBrush("AppInputBorderBrush");
        }
    }

    /// <summary>새 플레이리스트(점선 근사) hover — 테두리·텍스트 모두 코럴 (시안).</summary>
    private void OnDashedButtonPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button { IsEnabled: true } button)
        {
            button.BorderBrush = TokenBrush("AppAccentBrush");
            button.Foreground = TokenBrush("AppAccentBrush");
        }
    }

    private void OnDashedButtonPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.BorderBrush = TokenBrush("AppDashedBorderBrush");
            button.Foreground = TokenBrush("AppTextNavBrush");
        }
    }

    /// <summary>행 재생 버튼 hover — 테두리·글리프 코럴 (시안).</summary>
    private void OnRowPlayPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button { IsEnabled: true, Content: FontIcon icon } button)
        {
            button.BorderBrush = TokenBrush("AppAccentBrush");
            icon.Foreground = TokenBrush("AppAccentBrush");
        }
    }

    private void OnRowPlayPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button { Content: FontIcon icon } button)
        {
            button.BorderBrush = TokenBrush("AppInputBorderBrush");
            icon.Foreground = TokenBrush("AppChipTextBrush");
        }
    }

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

    /// <summary>공유 메뉴 — 항목 URL을 담아 우클릭한 행 앵커로 공유 팝업 표시 (share plan T1·D1).</summary>
    private void OnShareClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not PlaylistItemEntry entry)
        {
            return;
        }

        ShareUrlBox.Text = entry.Url;
        // sender는 MenuFlyoutItem이라 행 시각 요소가 아님 — 컨테이너 조회, 실패 시 목록 자체 폴백 (리뷰 m1)
        var anchor = ItemListView.ContainerFromItem(entry) as FrameworkElement ?? ItemListView;
        ShareFlyout.ShowAt(anchor);
    }

    /// <summary>복사 버튼 — 클립보드 복사 후 팝업을 닫고 결과를 알린다 (실패해도 앱 동작 유지).</summary>
    private void OnShareCopyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(ShareUrlBox.Text);
            Clipboard.SetContent(package);
            ShareFlyout.Hide();
            ViewModel.NotifyLinkCopied();
        }
        catch (Exception ex)
        {
            AppLog.Write($"링크 복사 중 오류: {ex.GetType().Name} {ex.Message}");
            ViewModel.NotifyLinkCopyFailed();
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

    /// <summary>행 재생/정지 토글 버튼 — 재생 중인 곡의 행이면 정지, 아니면 해당 항목부터 재생 (FR-18, stop-toggle plan D2).</summary>
    private async void OnPlayItemClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not PlaylistItemEntry item)
        {
            return;
        }

        try
        {
            await ViewModel.TogglePlayItemAsync(item);
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
