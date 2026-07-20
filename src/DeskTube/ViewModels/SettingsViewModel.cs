using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskTube.Models;
using DeskTube.Services;
using Microsoft.UI.Xaml.Controls;

namespace DeskTube.ViewModels;

/// <summary>
/// 설정 페이지 ViewModel (PRD FR-10·13, plan T2).
/// 각 항목은 변경 즉시 적용·저장한다 (볼륨·모니터·오디오 대상은 재생 중에도 반영).
/// 모니터 선택·배지는 공용 MonitorPanelViewModel에 위임한다 (restyle plan T4·D4 — 홈과 공유).
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private AppServices? _services;
    private readonly StartupService _startup = new();

    /// <summary>초기값 채우는 중 — 변경 적용(저장·서비스 호출) 억제.</summary>
    private bool _loading;

    /// <summary>StartupTask 실제 상태를 토글에 반영하는 중 — 변경 콜백 억제.</summary>
    private bool _autoStartUpdating;

    private readonly List<string?> _audioIds = [];

    /// <summary>화질 콤보 인덱스 ↔ 렌더 세로 해상도 (0 = 원본, part1 FR-13).</summary>
    private static readonly int[] QualityHeights = [0, 1440, 1080, 720, 480];

    /// <summary>언어 콤보 인덱스 ↔ 언어 코드 (null = 시스템 추종 — plan T7, AGENTS 다국어 규칙 3).</summary>
    private static readonly string?[] LanguageCodes = [null, "ko-KR", "en-US"];

    public SettingsViewModel()
    {
        MonitorPanel.MonitorsRefreshed += OnMonitorsRefreshed;
        MonitorPanel.NoticeRequested += OnPanelNoticeRequested;

        // partial property 초기값 (콤보 미선택 = -1, 변경 콜백은 음수 가드로 무시됨)
        QualityIndex = -1;
        AudioIndex = -1;
        LanguageIndex = -1;
        FitModeIndex = -1;

        // 아래 세 목록은 순서가 곧 계약이다 — 항목 추가·삭제·재배열 시 대응 배열도 같이 고친다.
        // QualityOptions ↔ QualityHeights, LanguageOptions ↔ LanguageCodes, FitModeOptions ↔ FitMode enum 값.
        QualityOptions =
        [
            Loc.Get("Quality_Original"),
            Loc.Get("Quality_1440"),
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

    /// <summary>모니터 카드 패널 (홈과 공유하는 공용 VM — 선택 저장·최소 1개 강제·배지 계산 담당).</summary>
    public MonitorPanelViewModel MonitorPanel { get; } = new();

    /// <summary>유튜브 계정 상태 패널 (FR-15) — 홈과 같은 전역 공유 인스턴스 (상태 정본 단일화).</summary>
    public AccountPanelViewModel Account => AccountPanelViewModel.Shared;

    public ObservableCollection<string> AudioOptions { get; } = [];

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
    public partial int QualityIndex { get; set; }

    [ObservableProperty]
    public partial bool ReduceMirrorQuality { get; set; }

    /// <summary>앱 시작 후 자동 재생 토글 (FR-19 — 기본 켜짐, 마지막 항목 재개).</summary>
    [ObservableProperty]
    public partial bool AutoPlayOnLaunch { get; set; }

    [ObservableProperty]
    public partial int FitModeIndex { get; set; }

    /// <summary>자막 표시 토글 (FR-20 — 기본 끔, 켬=강제 표시/끔=강제 숨김).</summary>
    [ObservableProperty]
    public partial bool CaptionsEnabled { get; set; }

    [ObservableProperty]
    public partial int AudioIndex { get; set; }

    [ObservableProperty]
    public partial bool PauseOnFullscreen { get; set; }

    [ObservableProperty]
    public partial bool PauseOnBatterySaver { get; set; }

    [ObservableProperty]
    public partial bool PauseOnSessionLock { get; set; }

    [ObservableProperty]
    public partial bool AutoStartEnabled { get; set; }

    /// <summary>토글 조작 가능 여부 — DisabledByUser/정책 관리 상태면 false (plan T4 Edge).</summary>
    [ObservableProperty]
    public partial bool AutoStartToggleAvailable { get; set; }

    [ObservableProperty]
    public partial string? AutoStartStatusText { get; set; }

    [ObservableProperty]
    public partial bool AutoStartStatusVisible { get; set; }

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
            // 재진입 — Unloaded의 Detach로 끊긴 모니터 변경 구독을 되살리고 목록 갱신 (Attach는 멱등)
            MonitorPanel.Attach(_services!);
            Account.Attach(_services!); // 공유 인스턴스 재연결 — 프로브 없이 문구만 재생성 (NFR-3 재진입 경제성)

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

    /// <summary>페이지 이탈 시 호출 — 대기 중이던 이벤트 구독 해제 (누수 방지, 패널 구독 포함).</summary>
    public void Detach()
    {
        App.ServicesInitialized -= OnServicesInitialized;
        MonitorPanel.Detach();
    }

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

        // 패널 연결이 MonitorsRefreshed → 오디오 콤보 재구성까지 수행 (자체 _loading 가드 있음)
        MonitorPanel.Attach(services);

        _loading = true;
        try
        {
            var settings = services.Settings;

            Volume = settings.Volume;
            IsMuted = settings.IsMuted;

            var qualityIdx = Array.IndexOf(QualityHeights, settings.QualityScaleHeight);
            QualityIndex = qualityIdx >= 0 ? qualityIdx : 0;

            ReduceMirrorQuality = settings.ReduceMirrorQuality;
            AutoPlayOnLaunch = settings.AutoPlayOnLaunch;
            FitModeIndex = (int)settings.FitMode;
            CaptionsEnabled = settings.CaptionsEnabled;

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
        // 이 로그가 재진입에 반복되면 캐시가 깨진 것 (acceptance: 2번째 진입 시 StartupTask 프로브 미실행 확인용)
        AppLog.Write("설정 최초 로드 — 자동 실행·로그인 상태 확인 (재진입은 모니터 목록·계정 상태만 갱신)");
        _ = RefreshAutoStartAsync();
        Account.Attach(services); // 공유 패널 연결 — 미프로브 상태였다면 최초 1회 세션 확인 (plan T5)
    }

    /// <summary>패널 목록 재구성 후 파생 UI 갱신 — 오디오 대상 콤보 재구성 (자체 로드 가드).</summary>
    private void OnMonitorsRefreshed(object? sender, EventArgs e)
    {
        if (_services is null)
        {
            return;
        }

        _loading = true;
        try
        {
            AudioOptions.Clear();
            _audioIds.Clear();
            AudioOptions.Add(Loc.Get("Settings_AudioAuto"));
            _audioIds.Add(null);
            foreach (var monitor in MonitorPanel.Monitors)
            {
                AudioOptions.Add(monitor.DisplayName);
                _audioIds.Add(monitor.Id);
            }

            var audioIdx = _audioIds.IndexOf(_services.Settings.AudioMonitorId);
            AudioIndex = audioIdx >= 0 ? audioIdx : 0;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>패널의 사용자 안내(최소 1개 차단 등)를 공용 토스트로 표시 (toast plan T2 — 자동 소멸).</summary>
    private void OnPanelNoticeRequested(object? sender, string message) =>
        ToastService.Show(message, InfoBarSeverity.Warning);

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

    partial void OnVolumeChanged(double value) =>
        Apply(() => _services!.Coordinator.SetVolumeAsync((int)value), "볼륨 적용");

    /// <summary>음소거 토글 (FR-5 UI 노출) — 저장·재생 반영은 기구현 SetMutedAsync가 담당. 배지 갱신은 MutedChanged 구독(공용 패널 VM)이 처리.</summary>
    partial void OnIsMutedChanged(bool value) =>
        Apply(() => _services!.Coordinator.SetMutedAsync(value), "음소거 적용");

    partial void OnAudioIndexChanged(int value)
    {
        if (value >= 0 && value < _audioIds.Count)
        {
            Apply(async () =>
            {
                await _services!.Coordinator.SetAudioMonitorAsync(_audioIds[value]);
                MonitorPanel.UpdateAudioBadges(); // 소리 배지 위치 즉시 이동 (시안)
            }, "오디오 대상 적용");
        }
    }

    partial void OnQualityIndexChanged(int value)
    {
        if (value >= 0 && value < QualityHeights.Length)
        {
            Apply(() => _services!.Coordinator.SetQualityScaleAsync(QualityHeights[value]), "화질 스케일 적용");
        }
    }

    /// <summary>미러 화질 하향 토글 (NFR-2) — 재생 중이면 미러 스케일 즉시 재계산.</summary>
    partial void OnReduceMirrorQualityChanged(bool value) =>
        Apply(() => _services!.Coordinator.SetReduceMirrorQualityAsync(value), "미러 화질 하향 적용");

    /// <summary>자막 표시 토글 (FR-20) — 재생 중이면 전 플레이어에 즉시 반영.</summary>
    partial void OnCaptionsEnabledChanged(bool value) =>
        Apply(() => _services!.Coordinator.SetCaptionsEnabledAsync(value), "자막 표시 적용");

    /// <summary>크기 모드 변경 (FR-16) — 재생 중이면 전 플레이어에 즉시 반영.</summary>
    partial void OnFitModeIndexChanged(int value)
    {
        if (value >= 0 && value < FitModeOptions.Count)
        {
            Apply(() => _services!.Coordinator.SetFitModeAsync((FitMode)value), "크기 모드 적용");
        }
    }

    /// <summary>
    /// 언어 변경 — 저장 완료 후 앱을 재시작해 전체 UI를 새 언어로 반영한다 (트레이·셸만 재생성하면 일부만 바뀜).
    /// 재시작 앱은 시작 시 저장된 언어를 선적용하므로 여기서 PrimaryLanguageOverride를 세션에 걸 필요가 없다.
    /// </summary>
    partial void OnLanguageIndexChanged(int value)
    {
        if (_loading || _services is null || value < 0 || value >= LanguageCodes.Length)
        {
            return;
        }

        var previousLanguage = _services.Settings.Language;
        _services.Settings.Language = LanguageCodes[value];
        _ = SaveAndRestartAsync();

        // 저장 완료를 보장한 뒤 재시작한다 — 저장 전에 프로세스가 종료되면 재시작 앱이 옛 언어를 읽는다.
        // 미관찰 예외 방지 — SaveSettingsAsync throw나 재시작 정리 중 예외도 로그로 남긴다 (Apply 헬퍼 관례).
        async Task SaveAndRestartAsync()
        {
            try
            {
                var result = await _services.Store.SaveSettingsAsync(_services.Settings);
                if (!result.IsSuccess)
                {
                    // 저장 실패 시 재시작하지 않고 메모리 값도 되돌린다 — 안 되돌리면 새 언어가
                    // 이후 다른 설정 저장에 편승해 디스크·UI와 어긋난 채 슬쩍 영속될 수 있다.
                    _services.Settings.Language = previousLanguage;
                    AppLog.Write($"언어 설정 저장 실패 — 재시작을 취소합니다: {result.Message}");
                    return;
                }

                if (Microsoft.UI.Xaml.Application.Current is App app)
                {
                    app.RestartForLanguageChange();
                }
            }
            catch (Exception ex)
            {
                AppLog.Write($"언어 변경 처리 중 오류: {ex.GetType().Name} {ex.Message}");
            }
        }
    }

    /// <summary>앱 시작 후 자동 재생 토글 (FR-19) — 저장만, 다음 실행부터 적용.</summary>
    partial void OnAutoPlayOnLaunchChanged(bool value)
    {
        if (_loading || _services is null)
        {
            return;
        }

        _services.Settings.AutoPlayOnLaunch = value;
        Apply(() => _services.Store.SaveSettingsAsync(_services.Settings), "앱 시작 자동 재생 설정 저장");
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
