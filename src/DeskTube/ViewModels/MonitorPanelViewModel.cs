using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DeskTube.Services;

namespace DeskTube.ViewModels;

/// <summary>
/// 모니터 카드 패널 공용 ViewModel (plan T4·D4) — 홈·설정 화면이 각자 인스턴스를 소유한다.
/// 열거·선택 토글(최소 1개 강제)·저장·오디오 배지 계산을 담당 (SettingsViewModel에서 이동).
/// 선택 상태의 정본은 AppSettings이므로 인스턴스가 여럿이어도 페이지 진입 시 Refresh로 동기화된다.
/// </summary>
public partial class MonitorPanelViewModel : ObservableObject
{
    private AppServices? _services;
    private Microsoft.UI.Dispatching.DispatcherQueue? _dispatcher;

    /// <summary>목록 재구성 중 — 선택 변경 콜백(저장·적용) 억제.</summary>
    private bool _loading;

    public ObservableCollection<MonitorChoice> Monitors { get; } = [];

    /// <summary>목록 재구성 완료 — 소비 화면이 파생 UI(오디오 콤보 등)를 재구성한다.</summary>
    public event EventHandler? MonitorsRefreshed;

    /// <summary>사용자 안내 요청 (최소 1개 차단 등) — 소비 화면의 InfoBar가 표시한다.</summary>
    public event EventHandler<string>? NoticeRequested;

    /// <summary>
    /// 서비스 연결 + 최초 목록 구성. UI 스레드에서 호출하는 전제 (MonitorsChanged 마셜링용
    /// DispatcherQueue를 여기서 캡처). 페이지 캐시 재진입의 중복 호출은 무시된다.
    /// </summary>
    public void Attach(AppServices services)
    {
        if (_services is not null)
        {
            return;
        }

        _services = services;
        _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        // 모니터 연결/분리 즉시 반영 (plan T4 Edge) — WndProc 스레드에서 발생하므로 UI로 마셜링
        services.Monitors.MonitorsChanged += OnMonitorsChanged;
        Refresh();
    }

    /// <summary>모니터 목록 재열거 + 저장된 선택(주 모니터 폴백 해석) 반영 + 배지 갱신.</summary>
    public void Refresh()
    {
        if (_services is null)
        {
            return;
        }

        _loading = true;
        try
        {
            var settings = _services.Settings;
            var monitors = _services.Monitors.GetMonitors();

            // 저장된 선택을 실제 적용 규칙(주 모니터 폴백)으로 해석해 그대로 표시
            var effective = MonitorService.ResolveTargets(monitors, settings.SelectedMonitorIds)
                .Select(m => m.Id)
                .ToHashSet();

            Monitors.Clear();
            var index = 1;
            foreach (var monitor in monitors)
            {
                var name = string.Format(Loc.Get("Settings_MonitorNameFormat"), index, monitor.Width, monitor.Height);
                if (monitor.IsPrimary)
                {
                    name += Loc.Get("Settings_MonitorPrimarySuffix");
                }

                var resolution = string.Format(Loc.Get("Monitor_ResolutionFormat"), monitor.Width, monitor.Height);
                if (monitor.IsPrimary)
                {
                    resolution += Loc.Get("Monitor_PrimarySuffix");
                }

                Monitors.Add(new MonitorChoice(
                    monitor.Id, index, name, resolution, monitor.IsPrimary,
                    effective.Contains(monitor.Id), OnMonitorSelectionChanged));
                index++;
            }
        }
        finally
        {
            _loading = false;
        }

        UpdateAudioBadges();
        MonitorsRefreshed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 오디오 배지 재계산 — 배지 = 선택됨 ∧ 오디오 대상 ∧ 비음소거 (시안 식).
    /// 대상 판정은 검증된 정적 로직(ResolveTargets/ResolveAudioTarget)을 재사용한다.
    /// </summary>
    public void UpdateAudioBadges()
    {
        if (_services is null)
        {
            return;
        }

        var settings = _services.Settings;
        var monitors = _services.Monitors.GetMonitors();
        var selectedIds = Monitors.Where(m => m.IsSelected).Select(m => m.Id).ToList();
        var targets = MonitorService.ResolveTargets(monitors, selectedIds);
        var audio = MonitorService.ResolveAudioTarget(targets, settings.AudioMonitorId);

        foreach (var choice in Monitors)
        {
            choice.ShowAudioBadge = !settings.IsMuted
                && audio is not null
                && choice.IsSelected
                && choice.Id == audio.Id;
        }
    }

    private void OnMonitorsChanged(object? sender, EventArgs e) => _dispatcher?.TryEnqueue(Refresh);

    private void OnMonitorSelectionChanged(MonitorChoice changed)
    {
        if (_loading || _services is null)
        {
            return;
        }

        var selected = Monitors.Where(m => m.IsSelected).Select(m => m.Id).ToList();
        if (selected.Count == 0)
        {
            // 마지막 체크 해제 차단 — 최소 1개 강제 (plan T4 Edge, 기존 동작 보존)
            changed.SuppressCallback = true;
            changed.IsSelected = true;
            changed.SuppressCallback = false;
            NoticeRequested?.Invoke(this, Loc.Get("Settings_MonitorMinOne"));
            return;
        }

        _services.Settings.SelectedMonitorIds = selected;
        UpdateAudioBadges();
        _ = ApplyAsync();

        async Task ApplyAsync()
        {
            try
            {
                await _services.Store.SaveSettingsAsync(_services.Settings);
                await _services.Coordinator.ApplySelectedMonitorsAsync(); // 재생 중이면 즉시 반영
            }
            catch (Exception ex)
            {
                AppLog.Write($"모니터 선택 적용 중 오류: {ex.GetType().Name} {ex.Message}");
            }
        }
    }
}
