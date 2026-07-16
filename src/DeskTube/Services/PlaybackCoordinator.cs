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

    private int _timeTicks;
    private int _healthFailures;
    private bool _handlingMonitorChange;
    private bool _monitorChangePending;

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

    public event EventHandler<PlaybackStatus>? StatusChanged;

    public AppSettings Settings => _settings;

    /// <summary>
    /// 플레이리스트 재생 시작 (PRD FR-3). 배경창·플레이어 생성은 원자적 — 하나라도 실패하면
    /// 생성분을 전부 정리하고 실패를 반환한다 (plan T7 Edge Case).
    /// </summary>
    public async Task<Result> StartAsync(Guid playlistId)
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
            Subscribe(player);
            player.SetQualityScale(_settings.QualityScaleHeight);
            _players[target.Id] = new PlayerEntry(target, player);
        }

        _audioMonitorId = MonitorService.ResolveAudioTarget(targets, _settings.AudioMonitorId)?.Id;
        _queue = new PlaybackQueue(playlist.Items, _settings.Mode);

        var first = _queue.Start();
        if (first is null)
        {
            CleanupAll();
            return Result.Fail(ErrorCode.InvalidInput, "플레이리스트에 재생할 영상이 없습니다.");
        }

        ApplyAudioRouting();
        LoadAll(first);

        _userPaused = false;
        _policyPaused = false;
        _healthFailures = 0;
        SetStatus(PlaybackStatus.Playing);

        _settings.LastPlaylistId = playlistId;
        await _store.SaveSettingsAsync(_settings);
        return Result.Ok();
    }

    /// <summary>재생 정지 — 플레이어·배경창 전부 정리하고 원래 배경화면 복구 (PRD FR-9 정지).</summary>
    public Task StopAsync()
    {
        CleanupAll();
        _queue = null;
        SetStatus(PlaybackStatus.Stopped);
        return Task.CompletedTask;
    }

    /// <summary>사용자 일시정지 (트레이·UI).</summary>
    public void Pause()
    {
        if (Status != PlaybackStatus.Playing)
        {
            return;
        }

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

        _userPaused = false;
        _policyPaused = false;
        PlayAll();
        SetStatus(PlaybackStatus.Playing);
    }

    /// <summary>정책 일시정지 (NFR-1 — T8 PowerPolicyService가 호출). 사용자 정지와 독립 상태.</summary>
    public void PolicyPause()
    {
        _policyPaused = true;
        if (Status == PlaybackStatus.Playing)
        {
            PauseAll();
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
            PlayAll();
            SetStatus(PlaybackStatus.Playing);
        }
    }

    public async Task SetVolumeAsync(int volume)
    {
        _settings.Volume = Math.Clamp(volume, 0, 100);
        ApplyAudioRouting();
        await _store.SaveSettingsAsync(_settings);
    }

    public async Task SetMutedAsync(bool muted)
    {
        _settings.IsMuted = muted;
        ApplyAudioRouting();
        await _store.SaveSettingsAsync(_settings);
    }

    public async Task SetAudioMonitorAsync(string? monitorId)
    {
        _settings.AudioMonitorId = monitorId;
        _audioMonitorId = MonitorService.ResolveAudioTarget(
            [.. _players.Values.Select(e => e.Monitor)], monitorId)?.Id;
        ApplyAudioRouting();
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
        foreach (var entry in _players.Values)
        {
            entry.Player.SetQualityScale(_settings.QualityScaleHeight);
        }

        await _store.SaveSettingsAsync(_settings);
    }

    public void Dispose()
    {
        _monitors.MonitorsChanged -= OnMonitorsChanged;
        CleanupAll();
    }

    // ---- 플레이어 이벤트 (UI 스레드) ----

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

        // 임베드 금지(101/150) 등 재생 불가 — 마스터 기준으로 다음 곡 스킵 (PRD FR-1 Edge)
        if (IsMaster(sender) && !_suppressEnded)
        {
            _suppressEnded = true;
            FireAndForget(AdvanceAsync, "재생 불가 곡 스킵");
        }
    }

    private void OnPlayerTime(object? sender, double masterTime)
    {
        if (!IsMaster(sender))
        {
            return;
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
            // 순차 모드 끝 — 창 닫고 배경 복구 (plan T7 Edge Case)
            await StopAsync();
            return;
        }

        LoadAll(next);
    }

    private void LoadAll(PlaylistItem item)
    {
        _suppressEnded = true; // 새 곡 Playing 확인 전까지 이전 곡 Ended 무시
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

        Unsubscribe(old.Player);
        old.Player.Dispose();

        var created = await _playerFactory(old.Monitor);
        if (!created.IsSuccess || created.Value is null)
        {
            AppLog.Write($"플레이어 재생성 실패({monitorId}) — 해당 모니터 재생 제외");
            _wallpaper.Detach(monitorId);
            RefreshAudioTargetAfterRemoval();
            return;
        }

        var player = created.Value;
        Subscribe(player);
        player.SetQualityScale(_settings.QualityScaleHeight);
        _players[monitorId] = old with { Player = player };

        ResumeCurrentTrack(player);
        ApplyAudioRouting();
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
            Subscribe(player);
            player.SetQualityScale(_settings.QualityScaleHeight);
            _players[target.Id] = new PlayerEntry(target, player);
            ResumeCurrentTrack(player);
        }

        RefreshAudioTargetAfterRemoval();
    }

    /// <summary>새로 합류한 플레이어가 현재 곡을 마스터 시각으로 이어서 재생하게 한다 (재생성·모니터 추가 공통).</summary>
    private void ResumeCurrentTrack(IPlayerHost player)
    {
        var current = _queue?.Current;
        if (current is null || Status == PlaybackStatus.Stopped)
        {
            return;
        }

        player.Load(current.VideoId);
        var masterTime = MasterTime();
        if (masterTime > 0)
        {
            player.Seek(masterTime);
        }
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

    /// <summary>오디오 대상이 사라졌으면 주 모니터 우선으로 재결정 후 라우팅 재적용 (PRD FR-4 폴백).</summary>
    private void RefreshAudioTargetAfterRemoval()
    {
        _audioMonitorId = MonitorService.ResolveAudioTarget(
            [.. _players.Values.Select(p => p.Monitor)], _settings.AudioMonitorId)?.Id;
        ApplyAudioRouting();
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
}
