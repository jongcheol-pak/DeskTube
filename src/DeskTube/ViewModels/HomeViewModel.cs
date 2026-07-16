using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskTube.Services;
using Microsoft.UI.Xaml.Controls;

namespace DeskTube.ViewModels;

/// <summary>
/// 홈 — URL 입력·즉시 재생 (PRD FR-10, plan T2 Design ①).
/// 즉시 재생은 "빠른 재생" 플레이리스트(이름 고정)를 만들어 그 항목을 교체하는 방식 —
/// part1 Coordinator가 플레이리스트 단위로만 재생하므로 공개 계약을 바꾸지 않는다.
/// </summary>
public partial class HomeViewModel : ObservableObject
{
    public HomeViewModel()
    {
        // partial property는 초기화 식을 못 가짐 (MVVMTK0045 대응으로 필드 대신 채택)
        Url = string.Empty;
        NoticeSeverity = InfoBarSeverity.Informational;
    }

    [ObservableProperty]
    public partial string Url { get; set; }

    [ObservableProperty]
    public partial string? NoticeMessage { get; set; }

    [ObservableProperty]
    public partial bool IsNoticeOpen { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity NoticeSeverity { get; set; }

    [RelayCommand]
    private async Task PlayAsync()
    {
        var services = App.Services;
        if (services is null)
        {
            ShowNotice(Loc.Get("Common_NotReady"), InfoBarSeverity.Warning);
            return;
        }

        var parsed = YouTubeUrlParser.Parse(Url);
        if (!parsed.IsSuccess || parsed.Value is null)
        {
            // 입력은 유지 — 사용자가 고쳐서 재시도 (plan T2 Edge)
            ShowNotice(Loc.Get("Home_InvalidUrl"), InfoBarSeverity.Error);
            return;
        }

        var quickName = Loc.Get("Home_QuickPlaylistName");
        var playlist = services.Library.Playlists.FirstOrDefault(p => p.Name == quickName);
        if (playlist is null)
        {
            var created = services.Library.Create(quickName);
            if (!created.IsSuccess || created.Value is null)
            {
                AppLog.Write($"빠른 재생 리스트 생성 실패: {created.Message}");
                ShowNotice(Loc.Get("Home_PlayFailed"), InfoBarSeverity.Error);
                return;
            }

            playlist = created.Value;
        }

        // 항목을 이번 URL 1개로 교체
        playlist.Items.Clear();
        var added = services.Library.AddItem(playlist.Id, Url.Trim(), parsed.Value);
        if (!added.IsSuccess)
        {
            AppLog.Write($"빠른 재생 항목 추가 실패: {added.Message}");
            ShowNotice(Loc.Get("Home_PlayFailed"), InfoBarSeverity.Error);
            return;
        }

        await services.Library.SaveAsync();

        var startResult = await services.Coordinator.StartAsync(playlist.Id);
        if (!startResult.IsSuccess)
        {
            AppLog.Write($"즉시 재생 시작 실패: {startResult.Message}");
            ShowNotice(Loc.Get("Home_PlayFailed"), InfoBarSeverity.Error);
            return;
        }

        ShowNotice(Loc.Get("Home_PlayStarted"), InfoBarSeverity.Success);
    }

    private void ShowNotice(string message, InfoBarSeverity severity)
    {
        NoticeMessage = message;
        NoticeSeverity = severity;
        IsNoticeOpen = true;
    }
}
