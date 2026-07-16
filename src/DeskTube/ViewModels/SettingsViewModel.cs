using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskTube.Models;
using DeskTube.Services;

namespace DeskTube.ViewModels;

/// <summary>모니터 선택 항목 (재생 대상 다중 체크 — plan T2).</summary>
public partial class MonitorChoice : ObservableObject
{
    private readonly Action<MonitorChoice> _selectionChanged;

    public MonitorChoice(string id, string displayName, bool isSelected, Action<MonitorChoice> selectionChanged)
    {
        Id = id;
        DisplayName = displayName;
        _selectionChanged = selectionChanged;

        // 초기값 대입이 변경 콜백을 타지 않도록 억제 (partial property는 초기화 식 불가)
        SuppressCallback = true;
        IsSelected = isSelected;
        SuppressCallback = false;
    }

    public string Id { get; }

    public string DisplayName { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>마지막 체크 해제 차단의 되돌림 중 — 콜백 재진입 억제.</summary>
    internal bool SuppressCallback { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (!SuppressCallback)
        {
            _selectionChanged(this);
        }
    }
}

/// <summary>
/// 설정 페이지 ViewModel (PRD FR-10·13, plan T2).
/// 각 항목은 변경 즉시 적용·저장한다 (볼륨·모니터·오디오 대상은 재생 중에도 반영).
/// 자동 실행 토글은 T4(StartupService), 언어 선택은 T7(전환 절차)에서 추가된다.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private AppServices? _services;
    private readonly StartupService _startup = new();
    private YouTubeSessionService? _session;
    private bool _signedIn;

    /// <summary>초기값 채우는 중 — 변경 적용(저장·서비스 호출) 억제.</summary>
    private bool _loading;

    /// <summary>StartupTask 실제 상태를 토글에 반영하는 중 — 변경 콜백 억제.</summary>
    private bool _autoStartUpdating;

    private readonly List<string?> _audioIds = [];

    /// <summary>화질 콤보 인덱스 ↔ 렌더 세로 해상도 (0 = 원본, part1 FR-13).</summary>
    private static readonly int[] QualityHeights = [0, 1080, 720, 480];

    /// <summary>언어 콤보 인덱스 ↔ 언어 코드 (null = 시스템 추종 — plan T7, AGENTS 다국어 규칙 3).</summary>
    private static readonly string?[] LanguageCodes = [null, "ko-KR", "en-US"];

    public SettingsViewModel()
    {
        // partial property 초기값 (콤보 미선택 = -1, 변경 콜백은 음수 가드로 무시됨)
        ModeIndex = -1;
        QualityIndex = -1;
        AudioIndex = -1;
        LanguageIndex = -1;
        FitModeIndex = -1;

        ModeOptions =
        [
            Loc.Get("Mode_Sequential"),
            Loc.Get("Mode_Shuffle"),
            Loc.Get("Mode_Random"),
            Loc.Get("Mode_RepeatOne"),
            Loc.Get("Mode_RepeatAll"),
        ];
        QualityOptions =
        [
            Loc.Get("Quality_Original"),
            Loc.Get("Quality_1080"),
            Loc.Get("Quality_720"),
            Loc.Get("Quality_480"),
        ];
        FitModeOptions =
        [
            Loc.Get("Fit_Cover"),
            Loc.Get("Fit_Contain"),
            Loc.Get("Fit_Stretch"),
        ];
        LanguageOptions =
        [
            Loc.Get("Language_System"),
            Loc.Get("Language_Korean"),
            Loc.Get("Language_English"),
        ];
    }

    public ObservableCollection<MonitorChoice> Monitors { get; } = [];

    public ObservableCollection<string> AudioOptions { get; } = [];

    public IReadOnlyList<string> ModeOptions { get; }

    public IReadOnlyList<string> QualityOptions { get; }

    /// <summary>크기 모드 콤보 — 인덱스가 FitMode enum 값과 일치 (채움/맞춤/늘리기, FR-16).</summary>
    public IReadOnlyList<string> FitModeOptions { get; }

    public IReadOnlyList<string> LanguageOptions { get; }

    [ObservableProperty]
    public partial int LanguageIndex { get; set; }

    [ObservableProperty]
    public partial bool IsReady { get; set; }

    [ObservableProperty]
    public partial double Volume { get; set; }

    [ObservableProperty]
    public partial bool IsMuted { get; set; }

    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    [ObservableProperty]
    public partial int QualityIndex { get; set; }

    [ObservableProperty]
    public partial int FitModeIndex { get; set; }

    [ObservableProperty]
    public partial int AudioIndex { get; set; }

    [ObservableProperty]
    public partial bool PauseOnFullscreen { get; set; }

    [ObservableProperty]
    public partial bool PauseOnBatterySaver { get; set; }

    [ObservableProperty]
    public partial bool PauseOnSessionLock { get; set; }

    [ObservableProperty]
    public partial string? NoticeMessage { get; set; }

    [ObservableProperty]
    public partial bool IsNoticeOpen { get; set; }

    [ObservableProperty]
    public partial bool AutoStartEnabled { get; set; }

    /// <summary>토글 조작 가능 여부 — DisabledByUser/정책 관리 상태면 false (plan T4 Edge).</summary>
    [ObservableProperty]
    public partial bool AutoStartToggleAvailable { get; set; }

    [ObservableProperty]
    public partial string? AutoStartStatusText { get; set; }

    [ObservableProperty]
    public partial bool AutoStartStatusVisible { get; set; }

    [ObservableProperty]
    public partial string? AccountStatusText { get; set; }

    [ObservableProperty]
    public partial string? AccountButtonText { get; set; }

    /// <summary>세션 확인/전환 진행 중 — 버튼 중복 조작 방지.</summary>
    [ObservableProperty]
    public partial bool AccountActionAvailable { get; set; }

    /// <summary>로그인 창 열기 요청 — 창 생성은 View(SettingsPage)가 담당 (MVVM 경계).</summary>
    public event EventHandler? SignInRequested;

    /// <summary>최초 초기화 완료 — 이후 진입은 모니터 목록 갱신만 수행 (NFR-3, plan D7).</summary>
    private bool _initialized;

    /// <summary>
    /// 페이지 진입 시 호출. 서비스 준비 전이면 준비 완료 이벤트를 1회 대기한다.
    /// 페이지가 캐시되므로(NavigationCacheMode.Required) 재진입에서는 무거운 조회
    /// (StartupTask·세션 프로브)를 생략하고 모니터 목록만 재열거한다.
    /// </summary>
    public void Load()
    {
        if (App.Services is null)
        {
            App.ServicesInitialized += OnServicesInitialized;
            return;
        }

        if (_initialized)
        {
            RefreshMonitors();

            // 음소거는 트레이 메뉴에서도 바뀌므로 재진입 때 재동기화 (T2 — 트레이와 상태 일치)
            _loading = true;
            try
            {
                IsMuted = _services!.Settings.IsMuted;
            }
            finally
            {
                _loading = false;
            }

            return;
        }

        Populate(App.Services);
    }

    /// <summary>페이지 이탈 시 호출 — 대기 중이던 이벤트 구독 해제 (누수 방지).</summary>
    public void Detach() => App.ServicesInitialized -= OnServicesInitialized;

    private void OnServicesInitialized(object? sender, EventArgs e)
    {
        App.ServicesInitialized -= OnServicesInitialized;
        Populate(App.Services!);
    }

    /// <summary>최초 진입 1회 — 설정값·모니터 목록 로드 후 StartupTask·세션 상태를 비동기 조회.</summary>
    private void Populate(AppServices services)
    {
        _services = services;
        _initialized = true;
        _loading = true;
        try
        {
            var settings = services.Settings;
            RefreshMonitorListCore(settings);

            Volume = settings.Volume;
            IsMuted = settings.IsMuted;
            ModeIndex = (int)settings.Mode;

            var qualityIdx = Array.IndexOf(QualityHeights, settings.QualityScaleHeight);
            QualityIndex = qualityIdx >= 0 ? qualityIdx : 0;

            FitModeIndex = (int)settings.FitMode;

            PauseOnFullscreen = settings.PauseOnFullscreen;
            PauseOnBatterySaver = settings.PauseOnBatterySaver;
            PauseOnSessionLock = settings.PauseOnSessionLock;

            var languageIdx = Array.IndexOf(LanguageCodes, settings.Language);
            LanguageIndex = languageIdx >= 0 ? languageIdx : 0;

            IsReady = true;
        }
        finally
        {
            _loading = false;
        }

        // StartupTask·로그인 세션 상태는 비동기 조회 — 로드 플래그 밖에서 실제 상태로 채움.
        // 이 로그가 재진입에 반복되면 캐시가 깨진 것 (acceptance: 2번째 진입 시 프로브 미실행 확인용)
        AppLog.Write("설정 최초 로드 — 자동 실행·로그인 상태 확인 (재진입은 모니터 목록만 갱신)");
        _ = RefreshAutoStartAsync();
        _session = new YouTubeSessionService(App.MainWindowHandle);
        _ = RefreshSessionAsync();
    }

    /// <summary>재진입 시 모니터 구성 변화만 반영 (가벼움 — 목록 재열거).</summary>
    private void RefreshMonitors()
    {
        if (_services is null)
        {
            return;
        }

        _loading = true;
        try
        {
            RefreshMonitorListCore(_services.Settings);
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>모니터 체크 목록·오디오 대상 콤보 재구성 (호출측이 _loading 가드를 건다).</summary>
    private void RefreshMonitorListCore(AppSettings settings)
    {
        var monitors = _services!.Monitors.GetMonitors();

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

            Monitors.Add(new MonitorChoice(monitor.Id, name, effective.Contains(monitor.Id), OnMonitorSelectionChanged));
            index++;
        }

        // 오디오 대상 — "자동(주 모니터)" + 모니터 목록
        AudioOptions.Clear();
        _audioIds.Clear();
        AudioOptions.Add(Loc.Get("Settings_AudioAuto"));
        _audioIds.Add(null);
        for (var i = 0; i < monitors.Count; i++)
        {
            AudioOptions.Add(Monitors[i].DisplayName);
            _audioIds.Add(monitors[i].Id);
        }

        var audioIdx = _audioIds.IndexOf(settings.AudioMonitorId);
        AudioIndex = audioIdx >= 0 ? audioIdx : 0;
    }

    /// <summary>로그인 상태 갱신 — 페이지 로드·로그인 창 닫힘 후 호출 (세션 만료도 여기서 자동 반영).</summary>
    public async Task RefreshSessionAsync()
    {
        if (_session is null)
        {
            return;
        }

        AccountActionAvailable = false;
        AccountStatusText = Loc.Get("Settings_AccountChecking");
        try
        {
            _signedIn = await _session.IsSignedInAsync();
            AccountStatusText = Loc.Get(_signedIn ? "Settings_AccountSignedIn" : "Settings_AccountSignedOut");
            AccountButtonText = Loc.Get(_signedIn ? "Settings_SignOut" : "Settings_SignIn");
            AccountActionAvailable = true;
        }
        catch (Exception ex)
        {
            // 확인 실패(WebView2 런타임 문제 등) — 버튼 비활성으로 방어
            AppLog.Write($"로그인 상태 확인 실패: {ex.GetType().Name} {ex.Message}");
            AccountStatusText = Loc.Get("Settings_AccountUnknown");
            AccountActionAvailable = false;
        }
    }

    [RelayCommand]
    private async Task AccountActionAsync()
    {
        if (_session is null || _services is null)
        {
            return;
        }

        if (!_signedIn)
        {
            SignInRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        AccountActionAvailable = false;
        try
        {
            await _session.SignOutAsync();
            _services.Coordinator.ReloadCurrentTrack(); // 재생 중이면 비로그인 세션으로 다시 로드 (plan D4)
        }
        catch (Exception ex)
        {
            AppLog.Write($"로그아웃 실패: {ex.GetType().Name} {ex.Message}");
        }

        await RefreshSessionAsync();
    }

    private async Task RefreshAutoStartAsync()
    {
        try
        {
            var state = await _startup.GetStateAsync();
            ApplyAutoStartState(state);
        }
        catch (Exception ex)
        {
            // 조회 실패(비패키지 실행 등) — 토글 비활성으로 방어
            AppLog.Write($"자동 실행 상태 조회 실패: {ex.GetType().Name} {ex.Message}");
            AutoStartToggleAvailable = false;
        }
    }

    /// <summary>StartupTask 실제 상태를 UI에 반영 (요청 거부 시 토글 되돌림 포함).</summary>
    private void ApplyAutoStartState(Windows.ApplicationModel.StartupTaskState state)
    {
        _autoStartUpdating = true;
        try
        {
            AutoStartEnabled = state is Windows.ApplicationModel.StartupTaskState.Enabled
                or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
            AutoStartToggleAvailable = state is Windows.ApplicationModel.StartupTaskState.Enabled
                or Windows.ApplicationModel.StartupTaskState.Disabled;
            AutoStartStatusText = state switch
            {
                Windows.ApplicationModel.StartupTaskState.DisabledByUser => Loc.Get("Settings_AutoStartDisabledByUser"),
                Windows.ApplicationModel.StartupTaskState.DisabledByPolicy
                    or Windows.ApplicationModel.StartupTaskState.EnabledByPolicy => Loc.Get("Settings_AutoStartByPolicy"),
                _ => null,
            };
            AutoStartStatusVisible = AutoStartStatusText is not null;
        }
        finally
        {
            _autoStartUpdating = false;
        }
    }

    partial void OnAutoStartEnabledChanged(bool value)
    {
        if (_loading || _autoStartUpdating || _services is null)
        {
            return;
        }

        Apply(async () =>
        {
            var state = await _startup.SetEnabledAsync(value);
            ApplyAutoStartState(state); // 거부됐으면(DisabledByUser 등) 실제 상태로 되돌림
        }, "자동 실행 설정");
    }

    private void OnMonitorSelectionChanged(MonitorChoice changed)
    {
        if (_loading || _services is null)
        {
            return;
        }

        var selected = Monitors.Where(m => m.IsSelected).Select(m => m.Id).ToList();
        if (selected.Count == 0)
        {
            // 마지막 체크 해제 차단 — 최소 1개 강제 (plan T2 Edge)
            changed.SuppressCallback = true;
            changed.IsSelected = true;
            changed.SuppressCallback = false;
            NoticeMessage = Loc.Get("Settings_MonitorMinOne");
            IsNoticeOpen = true;
            return;
        }

        IsNoticeOpen = false;
        _services.Settings.SelectedMonitorIds = selected;
        Apply(async () =>
        {
            await _services.Store.SaveSettingsAsync(_services.Settings);
            await _services.Coordinator.ApplySelectedMonitorsAsync(); // 재생 중이면 즉시 반영
        }, "모니터 선택 적용");
    }

    partial void OnVolumeChanged(double value) =>
        Apply(() => _services!.Coordinator.SetVolumeAsync((int)value), "볼륨 적용");

    /// <summary>음소거 토글 (FR-5 UI 노출) — 저장·재생 반영은 기구현 SetMutedAsync가 담당.</summary>
    partial void OnIsMutedChanged(bool value) =>
        Apply(() => _services!.Coordinator.SetMutedAsync(value), "음소거 적용");

    partial void OnAudioIndexChanged(int value)
    {
        if (value >= 0 && value < _audioIds.Count)
        {
            Apply(() => _services!.Coordinator.SetAudioMonitorAsync(_audioIds[value]), "오디오 대상 적용");
        }
    }

    partial void OnModeIndexChanged(int value)
    {
        if (value >= 0)
        {
            Apply(() => _services!.Coordinator.SetModeAsync((PlaybackMode)value), "재생 모드 적용");
        }
    }

    partial void OnQualityIndexChanged(int value)
    {
        if (value >= 0 && value < QualityHeights.Length)
        {
            Apply(() => _services!.Coordinator.SetQualityScaleAsync(QualityHeights[value]), "화질 스케일 적용");
        }
    }

    /// <summary>크기 모드 변경 (FR-16) — 재생 중이면 전 플레이어에 즉시 반영.</summary>
    partial void OnFitModeIndexChanged(int value)
    {
        if (value >= 0 && value < FitModeOptions.Count)
        {
            Apply(() => _services!.Coordinator.SetFitModeAsync((FitMode)value), "크기 모드 적용");
        }
    }

    /// <summary>언어 변경 (plan T7, AGENTS 다국어 규칙 3) — ① 저장 ② 오버라이드 ③~⑤는 App.ApplyLanguageChange.</summary>
    partial void OnLanguageIndexChanged(int value)
    {
        if (_loading || _services is null || value < 0 || value >= LanguageCodes.Length)
        {
            return;
        }

        var code = LanguageCodes[value];
        _services.Settings.Language = code;
        Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = code ?? string.Empty;

        Apply(() => _services.Store.SaveSettingsAsync(_services.Settings), "언어 설정 저장");

        if (Microsoft.UI.Xaml.Application.Current is App app)
        {
            app.ApplyLanguageChange(); // 트레이·셸 재생성 (이 페이지도 새 창에서 다시 만들어짐)
        }
    }

    partial void OnPauseOnFullscreenChanged(bool value) =>
        ApplyPausePolicy(s => s.PauseOnFullscreen = value);

    partial void OnPauseOnBatterySaverChanged(bool value) =>
        ApplyPausePolicy(s => s.PauseOnBatterySaver = value);

    partial void OnPauseOnSessionLockChanged(bool value) =>
        ApplyPausePolicy(s => s.PauseOnSessionLock = value);

    /// <summary>일시정지 정책 저장 + 현재 신호로 즉시 재평가 (part1 T8 Reevaluate).</summary>
    private void ApplyPausePolicy(Action<AppSettings> mutate)
    {
        if (_loading || _services is null)
        {
            return;
        }

        mutate(_services.Settings);
        _services.PowerPolicy.Reevaluate();
        Apply(() => _services.Store.SaveSettingsAsync(_services.Settings), "일시정지 정책 저장");
    }

    /// <summary>변경 적용 공통 경로 — 로드 중이면 무시, 실패는 로그만 (UI는 다음 조작에 지장 없음).</summary>
    private void Apply(Func<Task> operation, string context)
    {
        if (_loading || _services is null)
        {
            return;
        }

        _ = RunAsync();

        async Task RunAsync()
        {
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                AppLog.Write($"{context} 중 오류: {ex.GetType().Name} {ex.Message}");
            }
        }
    }
}
