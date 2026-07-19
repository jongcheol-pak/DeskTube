using DeskTube.Models;

namespace DeskTube.Services;

/// <summary>재생 전체 상태 (트레이·UI 표시용).</summary>
public enum PlaybackStatus
{
    Stopped,
    Playing,
    Paused,
}

/// <summary>
/// 재생 오케스트레이터 (PRD FR-3·4·5·7, plan T7·D4·D10).
/// 재생 상태의 단일 소유자 — 선택 모니터별 배경창·플레이어 수명, 큐 진행, 마스터-미러 동기,
/// 오디오 라우팅(대상 1개만 소리)을 담당한다.
/// 모든 공개 메서드는 UI 스레드에서 호출하는 전제이며, 외부 스레드 이벤트(모니터 변경)는
/// 주입된 dispatch로 마셜링된다. 플레이어 이벤트는 UI 스레드(WebView2)에서 온다.
/// </summary>
public sealed class PlaybackCoordinator : IDisposable
{
    /// <summary>동기 보정 기준 — 마스터 시각 이벤트 5회(≈5초)마다 검사, 1초 초과 시 보정 (plan D4).</summary>
    private const int DriftCheckEveryTicks = 5;
    private const double DriftToleranceSeconds = 1.0;
    private const int MaxHealthFailures = 3;

    /// <summary>미러 화질 하향 상한 — 렌더 세로 해상도 (NFR-2, plan D5).</summary>
    private const int MirrorQualityCapHeight = 720;

    /// <summary>볼륨 저장 디바운스 — 슬라이더 드래그의 연속 변경을 마지막 1회 저장으로 합침 (perf plan D5).</summary>
    private const int VolumeSaveDebounceMs = 500;

    /// <summary>재생 진행 확인 임계(초) — player.html의 PLAYBACK_PROGRESS_MIN과 동일 기준 (FR-1 보강).</summary>
    private const double PlaybackProgressMinSeconds = 1.0;

    private sealed record PlayerEntry(MonitorInfo Monitor, IPlayerHost Player);

    private readonly IMonitorService _monitors;
    private readonly IWallpaperHost _wallpaper;
    private readonly Func<MonitorInfo, Task<Result<IPlayerHost>>> _playerFactory;
    private readonly PlaylistLibrary _library;
    private readonly IStateStore _store;
    private readonly AppSettings _settings;
    private readonly Action<Action> _dispatch;

    private readonly Dictionary<string, PlayerEntry> _players = [];
    private PlaybackQueue? _queue;
    private string? _audioMonitorId;
    private bool _userPaused;
    private bool _policyPaused;

    /// <summary>곡 전환 직후 늦게 도착하는 이전 곡의 Ended 중복 처리 방지 (Playing 수신 시 해제).</summary>
    private bool _suppressEnded;

    /// <summary>재생 불가로 확인된 항목 집합 (FR-1) — 실제 재생 진행(시각 보고 ≥ 임계) 확인 시 비움.
    /// PLAYING 도달만으로 비우면 "PLAYING 도달 후 미진행(-3)" 곡들이 매번 집합을 비워 전 항목 재생 불가
    /// 정지가 영영 발동하지 않는다 (2026-07-18 수정). 서로 다른 항목 기준으로 전 항목을 덮으면 정지한다
    /// (연속 카운트 방식은 Random에서 조기 오탐·한곡반복에서 동일 곡 재시도 낭비 — 2026-07-17 수정).</summary>
    private readonly HashSet<Guid> _failedItemIds = [];

    /// <summary>일시정지 중 재생 불가 오류 발생 — 재개 시 다음 곡으로 스킵한다
    /// (loadVideoById는 즉시 재생을 시작하므로 일시정지 중 자동 전진은 소리를 다시 낸다 — 2026-07-17 수정).</summary>
    private bool _advanceAfterResume;

    /// <summary>에러 스킵 예약됨 — 같은 곡의 중복 에러 이벤트로 다중 스킵 방지 (다음 곡 로드 시 해제).</summary>
    private bool _errorAdvancePending;

    /// <summary>볼륨 저장 디바운스 예약 취소 — 새 변경이 이전 예약을 교체한다 (perf plan D5).</summary>
    private CancellationTokenSource? _volumeSaveCts;

    private int _timeTicks;
    private int _healthFailures;
    private bool _handlingMonitorChange;
    private bool _monitorChangePending;

    /// <summary>재생성 진행 중인 모니터 — 재생성 await 중 모니터 변경 처리가 같은 모니터에
    /// 두 번째 플레이어를 만들어 미해제 이중 재생이 되는 것을 방지한다 (2026-07-18 수정).</summary>
    private readonly HashSet<string> _recreatingMonitors = [];

    public PlaybackCoordinator(
        IMonitorService monitors,
        IWallpaperHost wallpaper,
        Func<MonitorInfo, Task<Result<IPlayerHost>>> playerFactory,
        PlaylistLibrary library,
        IStateStore store,
        AppSettings settings,
        Action<Action>? dispatch = null)
    {
        _monitors = monitors;
        _wallpaper = wallpaper;
        _playerFactory = playerFactory;
        _library = library;
        _store = store;
        _settings = settings;
        _dispatch = dispatch ?? (action => action());

        _monitors.MonitorsChanged += OnMonitorsChanged;
    }

    public PlaybackStatus Status { get; private set; } = PlaybackStatus.Stopped;

    /// <summary>지금 재생(일시정지 포함) 중인 플레이리스트 ID — 정지 상태면 null (재생 중 표시 정본).
    /// LastPlaylistId는 정지 후에도 남는 재개용 이력이라 별개다. StatusChanged 발화 전에 확정되므로
    /// 핸들러가 이벤트 시점에 항상 현재 값을 읽는다.</summary>
    public Guid? CurrentPlaylistId { get; private set; }

    /// <summary>지금 재생(일시정지 포함) 중인 곡(항목) ID — 정지 상태면 null (우측 항목 목록의 재생 중 표시 정본).
    /// LastItemId는 정지 후에도 남는 재개용 이력이라 별개다(CurrentPlaylistId↔LastPlaylistId와 대칭).
    /// 같은 리스트 내 곡 전환은 StatusChanged를 발화하지 않으므로 항목 표시는 CurrentItemChanged로 갱신한다.</summary>
    public Guid? CurrentItemId { get; private set; }

    public event EventHandler<PlaybackStatus>? StatusChanged;

    /// <summary>재생 중인 곡(항목) 전환 알림 — 값 정본은 CurrentItemId (우측 항목 목록 표시 갱신용).
    /// StatusChanged가 못 잡는 같은 리스트 내 곡 전환(Advance)까지 알린다. 구독자는 CurrentItemId를 읽는다.</summary>
    public event EventHandler? CurrentItemChanged;

    /// <summary>음소거 상태 변경 알림 — 값 정본은 Settings.IsMuted (홈·설정 배지 동기화용, 배지 plan T2).</summary>
    public event EventHandler? MutedChanged;

    /// <summary>전 항목 재생 불가로 정지했음 — 표시는 UI 계층이 결정한다
    /// (창이 숨겨진 트레이 전용 상태에서도 안내가 보여야 하므로 코디네이터는 표시 수단에 의존하지 않는다).</summary>
    public event EventHandler? AllItemsFailed;

    /// <summary>재생시간(총 길이)이 수집돼 항목에 캐시됐음 — 인자는 수집된 항목 ID (FR-18, 우측 목록 표시 갱신용).
    /// CurrentItemChanged 등 "정본(CurrentItemId) 읽기" 이벤트와 달리 ID를 인자로 전달한다: 마셜링 지연 중 곡이
    /// 전환되면 CurrentItemId가 다른 곡을 가리켜 구독자가 오갱신하므로, 수집 시점의 항목 ID를 고정해 넘긴다 (plan D9).</summary>
    public event EventHandler<Guid>? ItemDurationCaptured;

    public AppSettings Settings => _settings;

    /// <summary>
    /// 플레이리스트 재생 시작 (PRD FR-3). 배경창·플레이어 생성은 원자적 — 하나라도 실패하면
    /// 생성분을 전부 정리하고 실패를 반환한다 (plan T7 Edge Case).
    /// startItemId 지정 시 그 항목부터 시작한다 (FR-18 행 재생 — 미존재 시 모드별 기본 시작).
    /// </summary>
    public async Task<Result> StartAsync(Guid playlistId, Guid? startItemId = null)
    {
        if (Status != PlaybackStatus.Stopped)
        {
            await StopAsync();
        }

        var playlist = _library.Playlists.FirstOrDefault(p => p.Id == playlistId);
        if (playlist is null)
        {
            return Result.Fail(ErrorCode.NotFound, "플레이리스트를 찾을 수 없습니다.");
        }

        if (playlist.Items.Count == 0)
        {
            return Result.Fail(ErrorCode.InvalidInput, "플레이리스트에 재생할 영상이 없습니다.");
        }

        var targets = MonitorService.ResolveTargets(_monitors.GetMonitors(), _settings.SelectedMonitorIds);
        if (targets.Count == 0)
        {
            return Result.Fail(ErrorCode.EnvironmentFailure, "재생할 모니터를 찾을 수 없습니다.");
        }

        // 오디오 대상을 플레이어 생성 전에 확정 — 생성 루프의 미러 스케일 판정(EffectiveScaleFor)에 필요 (D5)
        _audioMonitorId = MonitorService.ResolveAudioTarget(targets, _settings.AudioMonitorId)?.Id;

        foreach (var target in targets)
        {
            var attached = _wallpaper.Attach(target);
            if (!attached.IsSuccess)
            {
                CleanupAll();
                return attached;
            }

            var created = await _playerFactory(target);
            if (!created.IsSuccess || created.Value is null)
            {
                CleanupAll();
                return Result.Fail(created.Code, created.Message ?? "동영상 플레이어를 준비하지 못했습니다.");
            }

            var player = created.Value;
            InitializePlayer(target.Id, player);
            _players[target.Id] = new PlayerEntry(target, player);
        }

        _queue = new PlaybackQueue(playlist.Items, _settings.Mode);

        var first = _queue.Start(startItemId);
        if (first is null)
        {
            CleanupAll();
            return Result.Fail(ErrorCode.InvalidInput, "플레이리스트에 재생할 영상이 없습니다.");
        }

        ApplyAudioRouting();
        AppLog.Write($"재생 시작: '{playlist.Name}' (항목 {playlist.Items.Count}개, 모드 {_settings.Mode}, 음소거 {_settings.IsMuted}, 볼륨 {_settings.Volume})");
        // 리스트·항목 정본을 이벤트 발화 전에 확정 — LoadAll이 CurrentItemChanged를, SetStatus가 StatusChanged를
        // 발화하며, 두 핸들러 모두 시점에 CurrentPlaylistId를 현재 값으로 읽어야 한다 (다른 리스트 전환 시 이전 값 오독 방지).
        CurrentPlaylistId = playlistId;
        LoadAll(first);

        _userPaused = false;
        _policyPaused = false;
        _healthFailures = 0;
        _failedItemIds.Clear();
        _errorAdvancePending = false;
        _advanceAfterResume = false;
        SetStatus(PlaybackStatus.Playing);

        _settings.LastPlaylistId = playlistId;
        await _store.SaveSettingsAsync(_settings);
        return Result.Ok();
    }

    /// <summary>
    /// 마지막 재생 리스트를 마지막 항목부터 재개한다 (FR-8·FR-19 — 앱 자동 시작·트레이 재생 공용 경로).
    /// 재생할 리스트가 없으면(미기록·삭제·빈 리스트) NotFound 실패를 반환하고, 안내 방식은 호출측이 결정한다.
    /// </summary>
    public async Task<Result> StartLastAsync()
    {
        var lastId = _settings.LastPlaylistId;
        var playlist = lastId.HasValue
            ? _library.Playlists.FirstOrDefault(p => p.Id == lastId.Value)
            : null;
        if (playlist is null || playlist.Items.Count == 0)
        {
            return Result.Fail(ErrorCode.NotFound, "재생할 마지막 플레이리스트가 없습니다.");
        }

        // 항목이 삭제됐으면 PlaybackQueue.Start가 무시하고 리스트 기본 시작 (FR-19 Edge)
        return await StartAsync(playlist.Id, _settings.LastItemId);
    }

    /// <summary>재생 정지 — 플레이어·배경창 전부 정리하고 원래 배경화면 복구 (PRD FR-9 정지).</summary>
    public Task StopAsync()
    {
        AppLog.Write("재생 정지");
        CleanupAll();
        _queue = null;
        CurrentPlaylistId = null; // StatusChanged(Stopped) 핸들러가 읽으므로 발화 전에 해제
        SetCurrentItem(null); // 재생 중 항목 표시 해제 — 전 행 순위 복귀
        SetStatus(PlaybackStatus.Stopped);
        Interop.ProcessInterop.TrimWorkingSet(); // 유휴 진입 — 워킹셋 OS 반환 (NFR-2, plan D6)
        return Task.CompletedTask;
    }

    /// <summary>사용자 일시정지 (트레이·UI).</summary>
    public void Pause()
    {
        if (Status != PlaybackStatus.Playing)
        {
            return;
        }

        AppLog.Write("일시정지(사용자)");
        _userPaused = true;
        PauseAll();
        SetStatus(PlaybackStatus.Paused);
    }

    /// <summary>사용자 재개 — 정책 일시정지 상태도 함께 해제한다 (수동 명령 우선 — plan T8 Edge Case).</summary>
    public void Resume()
    {
        if (Status != PlaybackStatus.Paused)
        {
            return;
        }

        AppLog.Write("재개(사용자)");
        _userPaused = false;
        _policyPaused = false;
        ResumeOrSkipFailed();
        SetStatus(PlaybackStatus.Playing);
    }

    /// <summary>정책 일시정지 (NFR-1 — T8 PowerPolicyService가 호출). 사용자 정지와 독립 상태.
    /// 배경이 가려지거나 절전 상황이므로 플레이어를 절전시켜 CPU·메모리를 회수한다 (perf plan D1).</summary>
    public void PolicyPause()
    {
        // 진단 로그 — "재생이 소리 없이 멈춤" 조사에서 정책 경로가 무로그라 원인 판별이 불가능했다 (2026-07-18)
        AppLog.Write($"정책 일시정지 요청 (현재 상태 {Status} — {(Status == PlaybackStatus.Playing ? "일시정지+절전 수행" : "기록만")})");
        _policyPaused = true;
        if (Status == PlaybackStatus.Playing)
        {
            PauseAll();
            SuspendAll();
            SetStatus(PlaybackStatus.Paused);
        }
    }

    /// <summary>정책 해제 — 정책이 걸어둔 일시정지만 풀며, 사용자가 직접 정지한 상태면 재생하지 않는다.</summary>
    public void PolicyResume()
    {
        if (!_policyPaused)
        {
            return; // 정책 일시정지 중이 아니면 무시 (사용자 정지 상태를 임의로 풀지 않음)
        }

        _policyPaused = false;
        if (Status == PlaybackStatus.Paused && !_userPaused)
        {
            AppLog.Write("정책 재개 — 재생 계속");
            ResumeOrSkipFailed();
            SetStatus(PlaybackStatus.Playing);
        }
        else
        {
            AppLog.Write($"정책 재개 요청 무시 (상태 {Status}, 사용자 정지 {_userPaused})");
        }
    }

    /// <summary>재개 공통 — 일시정지 중 재생 불가 오류가 있었으면 에러 난 곡 재생 대신 다음 곡으로 스킵한다.
    /// 초입에서 절전을 해제한다 — 사용자 Resume·PolicyResume 둘 다 이 경로를 경유한다 (perf plan D4).</summary>
    private void ResumeOrSkipFailed()
    {
        ResumeAllFromSuspend();
        if (_advanceAfterResume)
        {
            _advanceAfterResume = false;
            FireAndForget(AdvanceAsync, "재개 시 재생 불가 곡 스킵");
            return;
        }

        PlayAll();
    }

    /// <summary>볼륨 적용 (PRD FR-5) — 플레이어 반영은 즉시, 저장은 디바운스 예약 후 즉시 반환한다
    /// (슬라이더 드래그가 틱마다 settings.json을 쓰는 것 방지 — perf plan D5).</summary>
    public Task SetVolumeAsync(int volume)
    {
        _settings.Volume = Math.Clamp(volume, 0, 100);
        ApplyAudioRouting();

        _volumeSaveCts?.Cancel();
        _volumeSaveCts = new CancellationTokenSource();
        _ = SaveVolumeAfterDelayAsync(_volumeSaveCts.Token);
        return Task.CompletedTask;
    }

    /// <summary>디바운스 저장 본체 — 취소(예약 교체·종료)는 정상 경로라 로그하지 않는다.</summary>
    private async Task SaveVolumeAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(VolumeSaveDebounceMs, token);
            await _store.SaveSettingsAsync(_settings);
        }
        catch (OperationCanceledException)
        {
            // 새 변경으로 예약 교체 또는 Dispose — 정상 경로
        }
        catch (Exception ex)
        {
            AppLog.Write($"볼륨 저장 중 오류: {ex.GetType().Name} {ex.Message}");
        }
    }

    public async Task SetMutedAsync(bool muted)
    {
        _settings.IsMuted = muted;
        ApplyAudioRouting();
        MutedChanged?.Invoke(this, EventArgs.Empty); // 상태 반영 후·저장 전 — 구독자(배지)는 Settings.IsMuted를 읽는다
        await _store.SaveSettingsAsync(_settings);
    }

    public async Task SetAudioMonitorAsync(string? monitorId)
    {
        _settings.AudioMonitorId = monitorId;
        _audioMonitorId = MonitorService.ResolveAudioTarget(
            [.. _players.Values.Select(e => e.Monitor)], monitorId)?.Id;
        ApplyAudioRouting();
        ApplyEffectiveScales(); // 마스터↔미러가 바뀌면 하향 대상도 바뀐다 (D5 Edge)
        await _store.SaveSettingsAsync(_settings);
    }

    public async Task SetModeAsync(PlaybackMode mode)
    {
        _settings.Mode = mode;
        _queue?.SetMode(mode);
        await _store.SaveSettingsAsync(_settings);
    }

    /// <summary>
    /// 설정의 선택 모니터 변경을 재생 중에 반영한다 (part2 T2 — additive).
    /// 모니터 구성 변경과 동일한 재해석 경로(재진입 가드 포함)를 재사용한다.
    /// </summary>
    public Task ApplySelectedMonitorsAsync() => HandleMonitorsChangedAsync();

    /// <summary>
    /// 재생 중 플레이리스트 내용 변경(추가/삭제/순서)을 큐에 반영한다 (part2 T3 Edge — additive).
    /// 현재 곡이 남아 있으면 유지, 재생 중이던 곡이 삭제됐으면 다음 곡으로 진행,
    /// 리스트가 삭제·비워졌으면 정지한다.
    /// </summary>
    public async Task NotifyPlaylistChangedAsync(Guid playlistId)
    {
        if (_queue is null || Status == PlaybackStatus.Stopped || _settings.LastPlaylistId != playlistId)
        {
            return;
        }

        var playlist = _library.Playlists.FirstOrDefault(p => p.Id == playlistId);
        if (playlist is null || playlist.Items.Count == 0)
        {
            await StopAsync();
            return;
        }

        var currentId = _queue.Current?.Id;
        _queue.UpdateItems(playlist.Items);
        if (currentId is not null && _queue.Current?.Id != currentId)
        {
            await AdvanceAsync(); // 재생 중이던 곡이 삭제됨 — 다음 곡으로 (plan T3 Edge)
        }
    }

    /// <summary>
    /// 로그아웃 등 세션 변경 후 현재 곡을 전 플레이어에서 다시 로드한다 (part2 T5 — additive).
    /// 정지 상태면 아무것도 하지 않는다.
    /// </summary>
    public void ReloadCurrentTrack()
    {
        var current = _queue?.Current;
        if (current is null || Status == PlaybackStatus.Stopped)
        {
            return;
        }

        LoadAll(current);
    }

    public async Task SetQualityScaleAsync(int height)
    {
        _settings.QualityScaleHeight = Math.Max(0, height);
        ApplyEffectiveScales();
        await _store.SaveSettingsAsync(_settings);
    }

    /// <summary>미러 화질 하향 토글 (NFR-2, plan D5) — 전 플레이어 스케일 재계산 + 저장.</summary>
    public async Task SetReduceMirrorQualityAsync(bool enabled)
    {
        _settings.ReduceMirrorQuality = enabled;
        ApplyEffectiveScales();
        await _store.SaveSettingsAsync(_settings);
    }

    /// <summary>
    /// 플레이어별 유효 렌더 스케일 (D5 구현 강제 — 모든 SetQualityScale 호출이 이 헬퍼를 경유).
    /// 미러 하향이 켜져 있으면 오디오 대상이 아닌 모니터를 720 이하로 제한한다
    /// (설정 화질 0=원본이면 720으로, 그 외엔 min(설정, 720)).
    /// </summary>
    private int EffectiveScaleFor(string monitorId)
    {
        var height = _settings.QualityScaleHeight;
        if (_settings.ReduceMirrorQuality && monitorId != _audioMonitorId)
        {
            height = height == 0 ? MirrorQualityCapHeight : Math.Min(height, MirrorQualityCapHeight);
        }

        return height;
    }

    /// <summary>전 플레이어에 유효 스케일 재적용 (화질·미러 토글·오디오 대상 변경 후).</summary>
    private void ApplyEffectiveScales()
    {
        foreach (var entry in _players.Values)
        {
            entry.Player.SetQualityScale(EffectiveScaleFor(entry.Monitor.Id));
        }
    }

    /// <summary>동영상 크기 모드 변경 (PRD FR-16) — 전 플레이어 즉시 적용 + 저장.</summary>
    public async Task SetFitModeAsync(FitMode mode)
    {
        _settings.FitMode = mode;
        foreach (var entry in _players.Values)
        {
            entry.Player.SetFitMode(mode);
        }

        await _store.SaveSettingsAsync(_settings);
    }

    /// <summary>자막 표시 변경 (PRD FR-20) — 전 플레이어 즉시 적용 + 저장. 정지 상태면 저장만.</summary>
    public async Task SetCaptionsEnabledAsync(bool enabled)
    {
        _settings.CaptionsEnabled = enabled;
        foreach (var entry in _players.Values)
        {
            entry.Player.SetCaptionsEnabled(enabled);
        }

        await _store.SaveSettingsAsync(_settings);
    }

    public void Dispose()
    {
        _monitors.MonitorsChanged -= OnMonitorsChanged;

        // 디바운스 대기 중인 볼륨 저장이 있으면 종료 전 최종 저장 시도 — 실패·미완은 수용
        // (`.Wait()` 금지 규칙상 동기 대기 안 함, 실패해도 다음 설정 저장에 자연 회복 — perf plan D5).
        // 이미 저장이 끝난 예약도 토큰이 미취소로 남아 중복 저장 1회가 가능하나 같은 값 재기록이라 무해
        // (드물게 진행 중 저장과 임시 파일이 겹치면 한쪽이 IOException으로 실패할 수 있고, 그 경우도 수용).
        if (_volumeSaveCts is { IsCancellationRequested: false } pendingSave)
        {
            pendingSave.Cancel();
            FireAndForget(() => _store.SaveSettingsAsync(_settings), "종료 시 최종 볼륨 저장");
        }

        CleanupAll();
    }

    // ---- 플레이어 이벤트 (UI 스레드) ----

    /// <summary>
    /// 플레이어 공통 초기화 — 구독 + 플레이어별 설정 일괄 적용.
    /// 신규 시작·재생성·모니터 합류 세 경로가 공유한다 (설정 추가 시 한 곳만 수정 — 누락 방지).
    /// </summary>
    private void InitializePlayer(string monitorId, IPlayerHost player)
    {
        Subscribe(player);
        player.SetQualityScale(EffectiveScaleFor(monitorId));
        player.SetFitMode(_settings.FitMode);
        player.SetCaptionsEnabled(_settings.CaptionsEnabled);
    }

    private void Subscribe(IPlayerHost player)
    {
        player.StateChanged += OnPlayerStateChanged;
        player.ErrorOccurred += OnPlayerError;
        player.TimeUpdated += OnPlayerTime;
    }

    private void Unsubscribe(IPlayerHost player)
    {
        player.StateChanged -= OnPlayerStateChanged;
        player.ErrorOccurred -= OnPlayerError;
        player.TimeUpdated -= OnPlayerTime;
    }

    private void OnPlayerStateChanged(object? sender, PlayerState state)
    {
        if (!IsMaster(sender))
        {
            return;
        }

        if (state == PlayerState.Playing)
        {
            // 재생 불가 집합(_failedItemIds)은 여기서 비우지 않는다 — 권한 필요 영상은 PLAYING에
            // 도달하고도 진행되지 않아(-3 후속), PLAYING 기준으로 비우면 전 항목 재생 불가 정지가
            // 발동하지 못한다. 초기화는 OnPlayerTime의 실제 진행 확인 시점 (FR-1 보강, 2026-07-18 수정).
            _suppressEnded = false;
        }
        else if (state == PlayerState.Ended && !_suppressEnded && Status == PlaybackStatus.Playing)
        {
            _suppressEnded = true; // 늦게 도착하는 중복 Ended 무시 (다음 곡 Playing까지)
            FireAndForget(AdvanceAsync, "다음 곡 진행");
        }
    }

    private void OnPlayerError(object? sender, PlayerError error)
    {
        if (Status == PlaybackStatus.Stopped)
        {
            return;
        }

        if (error.Code == -2)
        {
            // WebView2 프로세스 실패 — 해당 플레이어만 재생성 1회 시도 (plan T6 Edge Case 위임분)
            var entry = _players.Values.FirstOrDefault(e => ReferenceEquals(e.Player, sender));
            if (entry is not null)
            {
                FireAndForget(() => RecreatePlayerAsync(entry.Monitor.Id), "플레이어 재생성");
            }

            return;
        }

        // 임베드 금지(101/150) 등 재생 불가 — 마스터 기준으로 다음 곡 스킵 (PRD FR-1 Edge).
        // 가드는 Ended 억제(_suppressEnded)와 분리 — 곡 시작 직후(Playing 이전) 에러도 스킵해야 한다 (FR-1 보강).
        if (IsMaster(sender) && !_errorAdvancePending && _queue is not null)
        {
            _errorAdvancePending = true;
            if (_queue.Current is { } current)
            {
                _failedItemIds.Add(current.Id);
            }

            // 전 항목 재생 불가(서로 다른 항목 기준) 또는 한곡반복(다음 곡도 같은 곡 — 재시도 무의미)
            // — 무한 재로드 방지: 정지 후 안내 (FR-1). 표시는 AllItemsFailed 구독자(UI 계층) 몫.
            if (_failedItemIds.Count >= _queue.Count || _queue.Mode == PlaybackMode.RepeatOne)
            {
                AppLog.Write($"재생 불가 항목 {_failedItemIds.Count}/{_queue.Count}개 — 재생 가능한 항목이 없어 정지합니다.");
                FireAndForget(async () =>
                {
                    await StopAsync();
                    AllItemsFailed?.Invoke(this, EventArgs.Empty);
                }, "전 항목 재생 불가 정지");
                return;
            }

            if (Status == PlaybackStatus.Paused)
            {
                // 일시정지 중 자동 전진 금지 — loadVideoById가 즉시 재생을 시작해 일시정지 상태를
                // 깨고 소리를 낸다 (정책 일시정지 포함). 재개 시점에 스킵한다.
                _advanceAfterResume = true;
                return;
            }

            _suppressEnded = true; // 스킵 전환 중 이전 곡의 늦은 Ended 무시 (기존 동작 유지)
            FireAndForget(AdvanceAsync, "재생 불가 곡 스킵");
        }
    }

    private void OnPlayerTime(object? sender, double masterTime)
    {
        if (!IsMaster(sender))
        {
            return;
        }

        if (masterTime >= PlaybackProgressMinSeconds && _failedItemIds.Count > 0)
        {
            _failedItemIds.Clear(); // 실제 재생 진행 확인 — 재생 불가 항목 집합 초기화 (FR-1)
        }

        // 재생시간 수집 (FR-18) — 실제 진행 확인 시점에 현재 곡의 총 길이를 항목에 캐시한다.
        // duration >= 1.0 가드로 라이브(0)·미보고 오수집을 막고(재생 불가 영상은 masterTime<1.0이라 애초에 미도달),
        // _queue.Current는 라이브러리 모델과 동일 객체 참조라 값 갱신이 곧 영속 대상 갱신이다.
        if (masterTime >= PlaybackProgressMinSeconds
            && sender is IPlayerHost master
            && master.CurrentDuration >= PlaybackProgressMinSeconds
            && _queue?.Current is { } current
            && current.Id == CurrentItemId)
        {
            var seconds = (int)Math.Round(master.CurrentDuration);
            // 저장값과 2초 이상 차이날 때만 갱신 — 곡당 저장·발화를 1회로 억제(미세 오차 반복 저장 방지, D8)
            if (Math.Abs(seconds - current.DurationSeconds) >= 2)
            {
                current.DurationSeconds = seconds;
                FireAndForget(() => _library.SaveAsync(), "재생시간 저장");
                ItemDurationCaptured?.Invoke(this, current.Id);
            }
        }

        _timeTicks++;
        if (_timeTicks % DriftCheckEveryTicks != 0)
        {
            return;
        }

        // 마스터-미러 동기 보정 (plan D4)
        foreach (var entry in _players.Values)
        {
            if (entry.Monitor.Id != _audioMonitorId &&
                Math.Abs(entry.Player.CurrentTime - masterTime) > DriftToleranceSeconds)
            {
                entry.Player.Seek(masterTime);
            }
        }

        CheckWallpaperHealth();
    }

    // ---- 내부 동작 ----

    private bool IsMaster(object? sender) =>
        _audioMonitorId is not null &&
        _players.TryGetValue(_audioMonitorId, out var entry) &&
        ReferenceEquals(entry.Player, sender);

    private async Task AdvanceAsync()
    {
        var next = _queue?.Next();
        if (next is null)
        {
            // 재생할 항목 없음(빈 목록 — 재생 중 전 항목 삭제 등) — 창 닫고 배경 복구.
            // 모드별 끝 도달은 null을 반환하지 않는다 (전 모드 순환 — FR-7).
            await StopAsync();
            return;
        }

        LoadAll(next);
        await _store.SaveSettingsAsync(_settings); // 곡 전환 시 LastItemId 영속 (FR-19 — StartAsync는 자체 저장에 편승)
    }

    private void LoadAll(PlaylistItem item)
    {
        // 항목 재생 시작 단일 경로 — 마지막 재생 항목을 기록해 앱 시작 시 재개에 쓴다 (FR-19).
        // 저장은 호출부 몫 (ReloadCurrentTrack은 현재 곡 재로드라 동일 값 재설정 — 저장 불요).
        _settings.LastItemId = item.Id;
        AppLog.Write($"곡 로드: '{item.Title}' ({item.VideoId})");
        SetCurrentItem(item.Id); // 재생 중 항목 표시 정본 갱신 — 곡 전환 시 CurrentItemChanged 발화

        _suppressEnded = true; // 새 곡 Playing 확인 전까지 이전 곡 Ended 무시
        _errorAdvancePending = false; // 새 곡의 재생 불가 에러는 다시 수용 (이전 곡 잔여 에러의 오도착은
                                      // 한 곡 추가 스킵으로 끝나며 전곡 불가 안전망이 무한 루프를 막는다)
        foreach (var entry in _players.Values)
        {
            entry.Player.Load(item.VideoId); // loadVideoById는 즉시 재생 시작
        }
    }

    private void PauseAll()
    {
        foreach (var entry in _players.Values)
        {
            entry.Player.Pause();
        }
    }

    private void SuspendAll()
    {
        foreach (var entry in _players.Values)
        {
            entry.Player.Suspend();
        }
    }

    /// <summary>절전 해제 — 미절전 플레이어에는 no-op (IPlayerHost 계약).</summary>
    private void ResumeAllFromSuspend()
    {
        foreach (var entry in _players.Values)
        {
            entry.Player.ResumeFromSuspend();
        }
    }

    private void PlayAll()
    {
        foreach (var entry in _players.Values)
        {
            entry.Player.Play();
        }
    }

    /// <summary>오디오 라우팅 (PRD FR-4) — 대상 1개만 소리, 나머지 강제 음소거.</summary>
    private void ApplyAudioRouting()
    {
        foreach (var entry in _players.Values)
        {
            var isAudioTarget = entry.Monitor.Id == _audioMonitorId;
            entry.Player.SetMuted(_settings.IsMuted || !isAudioTarget);
            if (isAudioTarget)
            {
                entry.Player.SetVolume(_settings.Volume);
            }
        }
    }

    private void CheckWallpaperHealth()
    {
        var health = _wallpaper.EnsureHealthy();
        if (health.IsSuccess)
        {
            _healthFailures = 0;
            return;
        }

        _healthFailures++;
        AppLog.Write($"배경창 상태 점검 실패 {_healthFailures}/{MaxHealthFailures}: {health.Message}");
        if (_healthFailures >= MaxHealthFailures)
        {
            // 백오프 재시도 소진 — 정지 (plan T7 Edge Case, T5 재시도 정책)
            FireAndForget(StopAsync, "배경창 소실 정지");
        }
    }

    private async Task RecreatePlayerAsync(string monitorId)
    {
        if (!_players.Remove(monitorId, out var old))
        {
            return;
        }

        _recreatingMonitors.Add(monitorId); // await 중 모니터 변경 처리의 중복 생성 방지 (이중 재생 수정)
        try
        {
            Unsubscribe(old.Player);
            old.Player.Dispose();

            // 1차: 기존 표면 위에 재생성 — 일반적인 브라우저 프로세스 소멸에서 표면 HWND는 유효하므로
            // 배경창을 먼저 파괴하지 않는다 (단일 모니터에서 매 복구마다 바탕화면이 번쩍이던 문제 수정).
            var created = await _playerFactory(old.Monitor);
            if (!created.IsSuccess || created.Value is null)
            {
                // 2차: 표면 HWND가 무효면 컨트롤러 생성이 0x80070578(Invalid window handle)로 실패한다
                // (2026-07-18 조사) — 배경창을 재생성한 뒤 한 번 더 시도한다.
                AppLog.Write($"플레이어 재생성 1차 실패({monitorId}) — 배경창 재생성 후 재시도: {created.Message}");
                _wallpaper.Detach(monitorId);
                var attached = _wallpaper.Attach(old.Monitor);
                if (!attached.IsSuccess)
                {
                    AppLog.Write($"플레이어 재생성 실패({monitorId}) — 배경창 재부착 실패: {attached.Message}");
                    await HandleRecreateGiveUpAsync();
                    return;
                }

                created = await _playerFactory(old.Monitor);
                if (!created.IsSuccess || created.Value is null)
                {
                    AppLog.Write($"플레이어 재생성 실패({monitorId}) — 해당 모니터 재생 제외");
                    _wallpaper.Detach(monitorId);
                    await HandleRecreateGiveUpAsync();
                    return;
                }
            }

            var player = created.Value;
            InitializePlayer(monitorId, player);
            _players[monitorId] = old with { Player = player };

            ResumeCurrentTrack(player);
            ApplyAudioRouting();
        }
        finally
        {
            _recreatingMonitors.Remove(monitorId);
        }
    }

    /// <summary>재생성 포기 후 정리 — 남은 플레이어가 없으면 정지한다. 방치하면 표면·플레이어 0개인 채
    /// Playing 상태가 지속되고, EnsureHealthy는 표면 0개를 정상으로 보므로 헬스체크로도 잡히지 않는다.</summary>
    private async Task HandleRecreateGiveUpAsync()
    {
        RefreshAudioTargetAfterRemoval();
        if (_players.Count == 0 && Status != PlaybackStatus.Stopped)
        {
            AppLog.Write("재생 가능한 플레이어가 없어 재생을 정지합니다.");
            await StopAsync();
        }
    }

    private void OnMonitorsChanged(object? sender, EventArgs e) =>
        _dispatch(() => FireAndForget(HandleMonitorsChangedAsync, "모니터 구성 변경 처리"));

    private async Task HandleMonitorsChangedAsync()
    {
        // WM_DISPLAYCHANGE는 한 토폴로지 변경에 여러 번 올 수 있다 — await 중 겹침 방지 (재진입 가드)
        if (_handlingMonitorChange)
        {
            _monitorChangePending = true;
            return;
        }

        _handlingMonitorChange = true;
        try
        {
            do
            {
                _monitorChangePending = false;
                await HandleMonitorsChangedCoreAsync();
            }
            while (_monitorChangePending);
        }
        finally
        {
            _handlingMonitorChange = false;
        }
    }

    private async Task HandleMonitorsChangedCoreAsync()
    {
        if (Status == PlaybackStatus.Stopped)
        {
            return;
        }

        var targets = MonitorService.ResolveTargets(_monitors.GetMonitors(), _settings.SelectedMonitorIds);
        if (targets.Count == 0)
        {
            await StopAsync();
            return;
        }

        var targetIds = targets.Select(t => t.Id).ToHashSet();

        // 분리된 모니터 정리 (plan T4 Edge Case)
        foreach (var removedId in _players.Keys.Where(id => !targetIds.Contains(id)).ToList())
        {
            _players.Remove(removedId, out var removed);
            Unsubscribe(removed!.Player);
            removed.Player.Dispose();
            _wallpaper.Detach(removedId);
        }

        // 기존 창 재배치 + 새 대상 추가 (해상도 변경·재연결)
        foreach (var target in targets)
        {
            if (_recreatingMonitors.Contains(target.Id))
            {
                continue; // 재생성 진행 중 — 부착·생성 모두 재생성 경로 소유 (이중 플레이어 방지)
            }

            if (_players.ContainsKey(target.Id))
            {
                _wallpaper.Attach(target); // 기존 표면 재배치
                continue;
            }

            var attached = _wallpaper.Attach(target);
            if (!attached.IsSuccess)
            {
                continue; // 부분 실패 — 나머지 대상은 유지 (재생 중 확장은 best-effort)
            }

            var created = await _playerFactory(target);
            if (!created.IsSuccess || created.Value is null)
            {
                _wallpaper.Detach(target.Id);
                continue;
            }

            var player = created.Value;
            InitializePlayer(target.Id, player);
            _players[target.Id] = new PlayerEntry(target, player);
            ResumeCurrentTrack(player);
        }

        RefreshAudioTargetAfterRemoval();
    }

    /// <summary>새로 합류한 플레이어가 현재 곡을 마스터 시각으로 이어서 재생하게 한다 (재생성·모니터 추가 공통).
    /// 시작 위치를 Load에 실어 보낸다 — load 직후 별도 Seek는 신선한 플레이어에서 유실돼(IFrame API 레이스)
    /// 합류 모니터가 처음부터 재생되던 문제를 막는다 (2026-07-19 동일 시각 이어재생 수정).</summary>
    private void ResumeCurrentTrack(IPlayerHost player)
    {
        var current = _queue?.Current;
        if (current is null || Status == PlaybackStatus.Stopped)
        {
            return;
        }

        var masterTime = MasterTime();
        player.Load(current.VideoId, masterTime > 0 ? masterTime : 0);
    }

    /// <summary>
    /// 백그라운드 작업 공통 진입점 (plan D11 — 관찰되지 않는 Task의 예외 유실 방지).
    /// 실패는 로그만 남긴다 — 상시 실행 앱은 백그라운드 예외 1회로 죽으면 안 된다.
    /// (async void 대신 discard로 처리 — AGENTS 비동기 컨벤션)
    /// </summary>
    private static void FireAndForget(Func<Task> operation, string context)
    {
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

    /// <summary>오디오 대상이 사라졌으면 주 모니터 우선으로 재결정 후 라우팅·스케일 재적용 (PRD FR-4 폴백).</summary>
    private void RefreshAudioTargetAfterRemoval()
    {
        _audioMonitorId = MonitorService.ResolveAudioTarget(
            [.. _players.Values.Select(p => p.Monitor)], _settings.AudioMonitorId)?.Id;
        ApplyAudioRouting();
        ApplyEffectiveScales(); // 오디오 대상이 바뀌었으면 미러 하향 대상도 재계산 (D5 Edge)
    }

    private double MasterTime() =>
        _audioMonitorId is not null && _players.TryGetValue(_audioMonitorId, out var master)
            ? master.Player.CurrentTime
            : 0;

    private void CleanupAll()
    {
        foreach (var entry in _players.Values)
        {
            Unsubscribe(entry.Player);
            entry.Player.Dispose();
        }

        _players.Clear();
        _wallpaper.DetachAll();
        _audioMonitorId = null;
        _timeTicks = 0;
    }

    private void SetStatus(PlaybackStatus status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;
        StatusChanged?.Invoke(this, status);
    }

    /// <summary>재생 중 곡 ID 확정 — ID가 실제로 바뀔 때만 갱신·발화한다 (SetStatus 가드 복제).
    /// ReloadCurrentTrack의 동일 곡 재로드는 값이 같아 무발화 — 불필요한 UI 갱신 방지.</summary>
    private void SetCurrentItem(Guid? itemId)
    {
        if (CurrentItemId == itemId)
        {
            return;
        }

        CurrentItemId = itemId;
        CurrentItemChanged?.Invoke(this, EventArgs.Empty);
    }
}
