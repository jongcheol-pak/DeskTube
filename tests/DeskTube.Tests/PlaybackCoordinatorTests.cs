using System.Globalization;
using DeskTube.Models;
using DeskTube.Services;
using Xunit;

namespace DeskTube.Tests;

/// <summary>
/// PlaybackCoordinator 오케스트레이션 검증 (plan T7 acceptance — 가짜 IPlayerHost/IWallpaperHost 주입).
/// </summary>
public sealed class PlaybackCoordinatorTests
{
    // ---- 가짜 구현 ----

    private sealed class FakeMonitorService : IMonitorService
    {
        public List<MonitorInfo> Monitors { get; } = [];
        public event EventHandler? MonitorsChanged;
        public IReadOnlyList<MonitorInfo> GetMonitors() => Monitors;
        public void RaiseChanged() => MonitorsChanged?.Invoke(this, EventArgs.Empty);
        public void Dispose() { }
    }

    private sealed class FakeWallpaperHost : IWallpaperHost
    {
        public List<string> Log { get; } = [];
        public HashSet<string> FailMonitorIds { get; } = [];
        public Result HealthResult { get; set; } = Result.Ok();
        private readonly HashSet<string> _attached = [];

        public IReadOnlyCollection<string> AttachedMonitorIds => _attached;

        public Result Attach(MonitorInfo monitor)
        {
            if (FailMonitorIds.Contains(monitor.Id))
            {
                Log.Add($"attach-fail:{monitor.Id}");
                return Result.Fail(ErrorCode.EnvironmentFailure, "테스트 실패 지정");
            }

            _attached.Add(monitor.Id);
            Log.Add($"attach:{monitor.Id}");
            return Result.Ok();
        }

        public void Detach(string monitorId)
        {
            _attached.Remove(monitorId);
            Log.Add($"detach:{monitorId}");
        }

        public void DetachAll()
        {
            _attached.Clear();
            Log.Add("detach-all");
        }

        public Result EnsureHealthy() => HealthResult;

        public void Dispose() { }
    }

    private sealed class FakePlayer : IPlayerHost
    {
        public List<string> Commands { get; } = [];
        public double CurrentTime { get; set; }
        public double CurrentDuration { get; set; }

        public event EventHandler? Ready;
        public event EventHandler<PlayerState>? StateChanged;
        public event EventHandler<PlayerError>? ErrorOccurred;
        public event EventHandler<double>? TimeUpdated;

        public Task<Result> InitializeAsync() => Task.FromResult(Result.Ok());
        // startSeconds > 0(합류·재생성 이어재생)이면 시작 위치를 태그해 실제 로드 인자를 검증 가능하게 한다.
        // 소수점 구분자가 ','인 로케일에서도 리터럴 '@42.0' 대조가 맞도록 InvariantCulture로 포맷한다.
        public void Load(string videoId, double startSeconds = 0) =>
            Commands.Add(startSeconds > 0
                ? $"load:{videoId}@{startSeconds.ToString("F1", CultureInfo.InvariantCulture)}"
                : $"load:{videoId}");
        public void Play() => Commands.Add("play");
        public void Pause() => Commands.Add("pause");
        public void SetVolume(int volume) => Commands.Add($"volume:{volume}");
        public void SetMuted(bool muted) => Commands.Add($"mute:{muted}");
        public void Seek(double seconds) => Commands.Add($"seek:{seconds:F1}");
        public void SetQualityScale(int height) => Commands.Add($"scale:{height}");
        public void SetFitMode(FitMode mode) => Commands.Add($"fit:{(int)mode}");
        public void SetCaptionsEnabled(bool enabled) => Commands.Add($"captions:{enabled}");
        public void Suspend() => Commands.Add("suspend");
        public void ResumeFromSuspend() => Commands.Add("resume-suspend");
        public void Dispose() => Commands.Add("dispose");

        public void RaiseState(PlayerState state) => StateChanged?.Invoke(this, state);
        public void RaiseError(int code) => ErrorOccurred?.Invoke(this, new PlayerError(code));

        // 실제 PlayerHost는 duration을 CurrentDuration에 캐시한 뒤 TimeUpdated를 발화한다 — 그 순서를 모사.
        // duration 미지정(0)은 기존 호출 동작 보존(CurrentDuration 미설정).
        public void RaiseTime(double time, double duration = 0)
        {
            if (duration > 0)
            {
                CurrentDuration = duration;
            }

            TimeUpdated?.Invoke(this, time);
        }
        public void RaiseReady() => Ready?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeStore : IStateStore
    {
        /// <summary>설정 저장 호출 기록 — 볼륨 디바운스 검증용 (perf plan T2).</summary>
        public int SettingsSaveCount { get; private set; }

        /// <summary>플레이리스트 저장 호출 기록 — 재생시간 수집 "저장 1회" 검증용 (item-duration plan T3).</summary>
        public int PlaylistSaveCount { get; private set; }

        public int LastSavedVolume { get; private set; }

        public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(new AppSettings());

        public Task<Result> SaveSettingsAsync(AppSettings settings)
        {
            SettingsSaveCount++;
            LastSavedVolume = settings.Volume; // settings는 공유 참조라 값을 캡처해 둔다
            return Task.FromResult(Result.Ok());
        }

        public Task<List<Playlist>> LoadPlaylistsAsync() => Task.FromResult(new List<Playlist>());

        public Task<Result> SavePlaylistsAsync(List<Playlist> playlists)
        {
            PlaylistSaveCount++;
            return Task.FromResult(Result.Ok());
        }
    }

    // ---- 테스트 하니스 ----

    private sealed class Harness
    {
        public FakeMonitorService Monitors { get; } = new();
        public FakeWallpaperHost Wallpaper { get; } = new();
        public Dictionary<string, FakePlayer> Players { get; } = [];
        public HashSet<string> FailPlayerMonitorIds { get; } = [];
        /// <summary>Library 내부 store — 재생시간 수집이 여기로 저장하므로 카운터 검증에 노출 (T3).</summary>
        public FakeStore LibraryStore { get; } = new();
        public PlaylistLibrary Library { get; }
        public FakeStore Store { get; } = new();
        public AppSettings Settings { get; } = new();
        public PlaybackCoordinator Coordinator { get; }
        public Playlist Playlist { get; }

        public Harness(int monitorCount = 2, int itemCount = 3)
        {
            Library = new PlaylistLibrary(LibraryStore);

            for (var i = 0; i < monitorCount; i++)
            {
                Monitors.Monitors.Add(new MonitorInfo($"MON-{i}", $@"\\.\DISPLAY{i + 1}", i * 1920, 0, 1920, 1080, IsPrimary: i == 0));
            }

            Playlist = Library.Create("테스트").Value!;
            for (var i = 0; i < itemCount; i++)
            {
                Library.AddItem(Playlist.Id, $"url{i}", $"video{i:D5}a");
            }

            Settings.SelectedMonitorIds = [.. Monitors.Monitors.Select(m => m.Id)];
            Settings.IsMuted = false; // 오디오 라우팅 테스트는 비음소거 전제 — 기본값(켬)과 무관하게 고정
            Settings.ReduceMirrorQuality = false; // 미러 하향 테스트는 "꺼짐→켬" 전환 전제 — 기본값(켬)과 무관하게 고정

            Coordinator = new PlaybackCoordinator(
                Monitors, Wallpaper, CreatePlayerAsync, Library, Store, Settings);
        }

        private Task<Result<IPlayerHost>> CreatePlayerAsync(MonitorInfo monitor)
        {
            if (FailPlayerMonitorIds.Contains(monitor.Id))
            {
                return Task.FromResult(Result<IPlayerHost>.Fail(ErrorCode.EnvironmentFailure, "테스트 실패 지정"));
            }

            var player = new FakePlayer();
            Players[monitor.Id] = player;
            return Task.FromResult(Result<IPlayerHost>.Ok(player));
        }

        public FakePlayer Master => Players["MON-0"]; // 기본: 주 모니터가 오디오 대상
    }

    // ---- 테스트 ----

    [Fact]
    public async Task 재생_시작_시_모든_대상에_부착하고_오디오는_주_모니터만_소리가_난다()
    {
        var h = new Harness(monitorCount: 2);

        var result = await h.Coordinator.StartAsync(h.Playlist.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(PlaybackStatus.Playing, h.Coordinator.Status);
        Assert.Contains("attach:MON-0", h.Wallpaper.Log);
        Assert.Contains("attach:MON-1", h.Wallpaper.Log);
        // FR-4: 오디오 대상(주 모니터)만 unmute+볼륨, 나머지 강제 음소거
        Assert.Contains("mute:False", h.Players["MON-0"].Commands);
        Assert.Contains($"volume:{h.Settings.Volume}", h.Players["MON-0"].Commands);
        Assert.Contains("mute:True", h.Players["MON-1"].Commands);
        // 첫 곡 로드는 전 플레이어 동일
        Assert.Contains("load:video00000a", h.Players["MON-0"].Commands);
        Assert.Contains("load:video00000a", h.Players["MON-1"].Commands);
    }

    [Fact]
    public async Task CurrentPlaylistId는_시작에_설정_일시정지에_유지_정지에_해제된다()
    {
        var h = new Harness();
        Guid? atPlayingEvent = null;
        h.Coordinator.StatusChanged += (_, status) =>
        {
            if (status == PlaybackStatus.Playing && atPlayingEvent is null)
            {
                atPlayingEvent = h.Coordinator.CurrentPlaylistId; // 발화 시점에 이미 확정돼야 한다 (표시 레이스 방지)
            }
        };

        Assert.Null(h.Coordinator.CurrentPlaylistId);

        await h.Coordinator.StartAsync(h.Playlist.Id);
        Assert.Equal(h.Playlist.Id, h.Coordinator.CurrentPlaylistId);
        Assert.Equal(h.Playlist.Id, atPlayingEvent);

        h.Coordinator.Pause();
        Assert.Equal(h.Playlist.Id, h.Coordinator.CurrentPlaylistId);

        await h.Coordinator.StopAsync();
        Assert.Null(h.Coordinator.CurrentPlaylistId);
    }

    [Fact]
    public async Task CurrentItemId는_시작에_첫_곡_곡전환에_다음_곡_정지에_해제된다()
    {
        var h = new Harness(itemCount: 3);
        var changed = 0;
        h.Coordinator.CurrentItemChanged += (_, _) => changed++;

        Assert.Null(h.Coordinator.CurrentItemId);

        await h.Coordinator.StartAsync(h.Playlist.Id);
        Assert.Equal(h.Playlist.Items[0].Id, h.Coordinator.CurrentItemId);
        Assert.Equal(1, changed); // 첫 곡 설정 발화

        h.Master.RaiseState(PlayerState.Playing); // 억제 해제
        h.Master.RaiseState(PlayerState.Ended);   // Advance → 다음 곡
        Assert.Equal(h.Playlist.Items[1].Id, h.Coordinator.CurrentItemId);
        Assert.Equal(2, changed); // 곡 전환 발화

        h.Coordinator.Pause(); // 일시정지 중에는 값 유지·무발화
        Assert.Equal(h.Playlist.Items[1].Id, h.Coordinator.CurrentItemId);
        Assert.Equal(2, changed);

        await h.Coordinator.StopAsync();
        Assert.Null(h.Coordinator.CurrentItemId);
        Assert.Equal(3, changed); // 정지 해제 발화
    }

    [Fact]
    public async Task 동일_곡_재로드는_CurrentItemChanged를_발화하지_않는다()
    {
        var h = new Harness(itemCount: 3);
        await h.Coordinator.StartAsync(h.Playlist.Id);
        var changedBefore = 0;
        h.Coordinator.CurrentItemChanged += (_, _) => changedBefore++;

        h.Coordinator.ReloadCurrentTrack(); // 현재 곡을 그대로 재로드 (세션 변경 후 경로)

        // 항목 ID가 그대로라 무발화 — 불필요한 UI 갱신 방지 (SetCurrentItem 가드)
        Assert.Equal(0, changedBefore);
        Assert.Equal(h.Playlist.Items[0].Id, h.Coordinator.CurrentItemId);
    }

    [Fact]
    public async Task 정책_일시정지는_일시정지_후_절전하고_해제는_절전_해제_후_재생한다()
    {
        var h = new Harness(monitorCount: 2);
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Coordinator.PolicyPause();

        // 전 플레이어 절전 + pause가 suspend보다 먼저 (영상 정지 후 절전)
        foreach (var player in h.Players.Values)
        {
            Assert.Contains("suspend", player.Commands);
            Assert.True(player.Commands.IndexOf("pause") < player.Commands.IndexOf("suspend"));
        }

        h.Coordinator.PolicyResume();

        // 절전 해제가 재생 재개보다 먼저 (절전 중 play는 처리가 보장되지 않음)
        foreach (var player in h.Players.Values)
        {
            Assert.Contains("resume-suspend", player.Commands);
            Assert.True(player.Commands.IndexOf("resume-suspend") < player.Commands.LastIndexOf("play"));
        }

        Assert.Equal(PlaybackStatus.Playing, h.Coordinator.Status);
    }

    [Fact]
    public async Task 연속_볼륨_변경은_즉시_적용되고_저장은_마지막_값_1회다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);
        var savesBefore = h.Store.SettingsSaveCount; // StartAsync 자체 저장분 제외

        await h.Coordinator.SetVolumeAsync(10);
        await h.Coordinator.SetVolumeAsync(20);
        await h.Coordinator.SetVolumeAsync(80);

        // 플레이어 반영은 즉시 3회, 저장은 디바운스 대기 중이라 아직 0회
        Assert.Contains("volume:80", h.Master.Commands);
        Assert.Equal(savesBefore, h.Store.SettingsSaveCount);

        await Task.Delay(1200); // 디바운스(500ms) 경과 — CI 지연 감안 여유
        Assert.Equal(savesBefore + 1, h.Store.SettingsSaveCount);
        Assert.Equal(80, h.Store.LastSavedVolume);
    }

    [Fact]
    public async Task 사용자_일시정지는_절전하지_않는다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Coordinator.Pause();

        Assert.DoesNotContain("suspend", h.Master.Commands);
    }

    [Fact]
    public async Task 정책_일시정지_중_사용자가_직접_재개해도_절전이_해제된다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);
        h.Coordinator.PolicyPause();

        h.Coordinator.Resume();

        Assert.Contains("resume-suspend", h.Master.Commands);
        Assert.Equal(PlaybackStatus.Playing, h.Coordinator.Status);
    }

    [Fact]
    public async Task 음소거_변경_시_MutedChanged가_발생하고_설정에_반영된다()
    {
        var h = new Harness();
        var raised = 0;
        h.Coordinator.MutedChanged += (_, _) => raised++;

        await h.Coordinator.SetMutedAsync(true);

        Assert.Equal(1, raised);
        Assert.True(h.Settings.IsMuted);
    }

    [Fact]
    public async Task 마스터_종료_이벤트가_모든_플레이어를_다음_곡으로_보낸다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Master.RaiseState(PlayerState.Playing); // 억제 해제
        h.Master.RaiseState(PlayerState.Ended);

        Assert.Contains("load:video00001a", h.Players["MON-0"].Commands);
        Assert.Contains("load:video00001a", h.Players["MON-1"].Commands);
    }

    [Fact]
    public async Task 중복_종료_이벤트는_한_번만_진행시킨다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Master.RaiseState(PlayerState.Playing);
        h.Master.RaiseState(PlayerState.Ended);
        h.Master.RaiseState(PlayerState.Ended); // 늦은 중복 — Playing 미도착 상태

        Assert.Single(h.Master.Commands, c => c == "load:video00001a");
        Assert.DoesNotContain("load:video00002a", h.Master.Commands);
    }

    [Fact]
    public async Task 슬레이브_종료_이벤트는_큐를_진행시키지_않는다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Players["MON-1"].RaiseState(PlayerState.Playing);
        h.Players["MON-1"].RaiseState(PlayerState.Ended);

        Assert.DoesNotContain("load:video00001a", h.Master.Commands);
    }

    [Fact]
    public async Task 재생_시작_시_마지막_재생_항목이_저장된다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);

        Assert.Equal(h.Playlist.Items[0].Id, h.Settings.LastItemId); // FR-19 — 첫 곡 기록
    }

    [Fact]
    public async Task 곡_진행_시_마지막_재생_항목이_갱신된다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Master.RaiseState(PlayerState.Playing);
        h.Master.RaiseState(PlayerState.Ended);

        Assert.Equal(h.Playlist.Items[1].Id, h.Settings.LastItemId); // FR-19 — 다음 곡으로 갱신
    }

    [Fact]
    public async Task 순차_마지막_곡_종료_시_처음부터_반복한다()
    {
        var h = new Harness(itemCount: 1);
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Master.RaiseState(PlayerState.Playing);
        h.Master.RaiseState(PlayerState.Ended);

        // 끝에서 정지하지 않고 첫 곡을 다시 로드한다 (FR-7 — 1곡 리스트는 같은 곡 재로드)
        Assert.Equal(2, h.Master.Commands.Count(c => c == "load:video00000a"));
        Assert.NotEqual(PlaybackStatus.Stopped, h.Coordinator.Status);
        Assert.DoesNotContain("detach-all", h.Wallpaper.Log);
    }

    [Fact]
    public async Task 임베드_금지_오류는_다음_곡으로_스킵한다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Master.RaiseState(PlayerState.Playing);
        h.Master.RaiseError(150);

        Assert.Contains("load:video00001a", h.Players["MON-0"].Commands);
        Assert.Contains("load:video00001a", h.Players["MON-1"].Commands);
    }

    [Fact]
    public async Task 재생_미진행_오류는_다음_곡으로_스킵한다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);

        // player.html 시작 감시가 보내는 코드(-3) — onError 없이 멈추는 재생 불가 영상 대비 (FR-1 보강).
        h.Master.RaiseError(-3);

        Assert.Contains("load:video00001a", h.Players["MON-0"].Commands);
        Assert.Contains("load:video00001a", h.Players["MON-1"].Commands);
    }

    [Fact]
    public async Task Playing_도달_후_재생_미진행_오류도_다음_곡으로_스킵한다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);

        // 권한 필요 영상은 PLAYING 상태로 진입하고도 실제 재생이 진행되지 않는다(currentTime 정지).
        // player.html이 currentTime 미진행을 감지해 -3을 보내며, Playing 도달 후에도 스킵돼야 한다.
        h.Master.RaiseState(PlayerState.Playing);
        h.Master.RaiseError(-3);

        Assert.Contains("load:video00001a", h.Players["MON-0"].Commands);
    }

    [Fact]
    public async Task 전_곡이_Playing_도달_후_미진행이어도_정지한다()
    {
        var h = new Harness(itemCount: 2);
        var raised = 0;
        h.Coordinator.AllItemsFailed += (_, _) => raised++;
        await h.Coordinator.StartAsync(h.Playlist.Id);

        // 전 곡이 권한 필요 영상인 리스트: 곡마다 PLAYING에 도달하지만 진행되지 않아 -3이 온다.
        // PLAYING 도달만으로 실패 집합을 비우면 전 항목 재생 불가 정지가 영영 발동하지 않고
        // 8초 간격 무한 스킵 루프가 된다 (2026-07-18 수정 회귀 방지).
        h.Master.RaiseState(PlayerState.Playing);
        h.Master.RaiseError(-3); // 1곡째 → 2곡째 스킵
        h.Master.RaiseState(PlayerState.Playing);
        h.Master.RaiseError(-3); // 2곡째 — 서로 다른 2개 = 전 항목 재생 불가

        Assert.Equal(PlaybackStatus.Stopped, h.Coordinator.Status);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task 실제_재생_진행이_확인되면_재생_불가_집합이_초기화된다()
    {
        var h = new Harness(itemCount: 2);
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Master.RaiseError(-3);  // 1곡째 재생 불가 → 2곡째 스킵 (집합 {1곡째})
        h.Master.RaiseState(PlayerState.Playing);
        h.Master.RaiseTime(5.0);  // 2곡째 실제 진행 확인 — 집합 초기화
        h.Master.RaiseError(-3);  // 이후 2곡째 재생 불가 — 집합 {2곡째} 1개뿐이라 정지 없이 스킵

        Assert.Equal(PlaybackStatus.Playing, h.Coordinator.Status); // 전 항목 실패로 오판하지 않는다
        Assert.Equal(2, h.Master.Commands.Count(c => c == "load:video00000a")); // 1곡째로 순환 스킵
    }

    [Fact]
    public async Task 첫_곡부터_재생_불가면_Playing_이전에도_다음_곡으로_스킵한다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);

        // Playing 도달 전(곡 시작 직후) 에러 — 로드 억제 가드와 무관하게 스킵돼야 한다 (FR-1 보강)
        h.Master.RaiseError(150);

        Assert.Contains("load:video00001a", h.Players["MON-0"].Commands);
    }

    [Fact]
    public async Task 연속_재생_불가_곡은_계속_스킵한다()
    {
        var h = new Harness(itemCount: 3);
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Master.RaiseError(150); // 1곡째 재생 불가 → 2곡째 로드
        h.Master.RaiseError(150); // 2곡째도 재생 불가 → 3곡째 로드

        Assert.Contains("load:video00002a", h.Players["MON-0"].Commands);
        Assert.Equal(PlaybackStatus.Playing, h.Coordinator.Status);
    }

    [Fact]
    public async Task 전_곡이_재생_불가면_정지한다()
    {
        var h = new Harness(itemCount: 2);
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Master.RaiseError(150); // 1곡째 → 2곡째 스킵
        h.Master.RaiseError(150); // 2곡째 — 연속 2개 = 전 항목 재생 불가

        Assert.Equal(PlaybackStatus.Stopped, h.Coordinator.Status);
        Assert.Contains("detach-all", h.Wallpaper.Log); // 배경 복구까지 정리됨
    }

    [Fact]
    public async Task 플레이어_생성_실패_시_원자적으로_정리하고_실패를_반환한다()
    {
        var h = new Harness();
        h.FailPlayerMonitorIds.Add("MON-1"); // 2번째 플레이어 실패

        var result = await h.Coordinator.StartAsync(h.Playlist.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlaybackStatus.Stopped, h.Coordinator.Status);
        Assert.Contains("detach-all", h.Wallpaper.Log);
        Assert.Contains("dispose", h.Players["MON-0"].Commands); // 먼저 만든 플레이어 정리
    }

    [Fact]
    public async Task 볼륨_변경은_오디오_대상에만_적용된다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);
        h.Players["MON-0"].Commands.Clear();
        h.Players["MON-1"].Commands.Clear();

        await h.Coordinator.SetVolumeAsync(80);

        Assert.Contains("volume:80", h.Players["MON-0"].Commands);
        Assert.DoesNotContain("volume:80", h.Players["MON-1"].Commands);
        Assert.Contains("mute:True", h.Players["MON-1"].Commands); // 라우팅 재적용에도 음소거 유지
    }

    [Fact]
    public async Task 오디오_대상_모니터가_분리되면_주_모니터로_폴백한다()
    {
        var h = new Harness();
        h.Settings.AudioMonitorId = "MON-1";
        await h.Coordinator.StartAsync(h.Playlist.Id);
        Assert.Contains("mute:False", h.Players["MON-1"].Commands);

        h.Monitors.Monitors.RemoveAt(1); // MON-1 분리
        h.Players["MON-0"].Commands.Clear();
        h.Monitors.RaiseChanged();

        Assert.Contains("detach:MON-1", h.Wallpaper.Log);
        Assert.Contains("dispose", h.Players["MON-1"].Commands);
        Assert.Contains("mute:False", h.Players["MON-0"].Commands); // 주 모니터로 오디오 폴백
    }

    [Fact]
    public async Task 마스터_시각_5틱마다_1초_초과_어긋난_슬레이브를_보정한다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);
        h.Players["MON-1"].CurrentTime = 10.0; // 슬레이브가 10초 지점

        for (var i = 0; i < 5; i++)
        {
            h.Master.RaiseTime(15.0); // 마스터는 15초 — 5초 어긋남
        }

        Assert.Contains("seek:15.0", h.Players["MON-1"].Commands);
        Assert.DoesNotContain(h.Master.Commands, c => c.StartsWith("seek:"));
    }

    [Fact]
    public async Task 배경창_점검_3회_연속_실패_시_정지한다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);
        h.Wallpaper.HealthResult = Result.Fail(ErrorCode.EnvironmentFailure, "테스트");

        for (var i = 0; i < 15; i++)
        {
            h.Master.RaiseTime(i); // 5틱마다 점검 → 15틱 = 실패 3회
        }

        Assert.Equal(PlaybackStatus.Stopped, h.Coordinator.Status);
    }

    [Fact]
    public async Task 일시정지와_재개가_전_플레이어에_전달된다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Coordinator.Pause();
        Assert.Equal(PlaybackStatus.Paused, h.Coordinator.Status);
        Assert.Contains("pause", h.Players["MON-0"].Commands);
        Assert.Contains("pause", h.Players["MON-1"].Commands);

        h.Coordinator.Resume();
        Assert.Equal(PlaybackStatus.Playing, h.Coordinator.Status);
        Assert.Contains("play", h.Players["MON-0"].Commands);
    }

    [Fact]
    public async Task 정책_일시정지는_사용자_정지와_독립적으로_동작한다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Coordinator.PolicyPause();
        Assert.Equal(PlaybackStatus.Paused, h.Coordinator.Status);

        h.Coordinator.PolicyResume();
        Assert.Equal(PlaybackStatus.Playing, h.Coordinator.Status);

        // 사용자 정지 중 정책 해제는 재생하지 않음
        h.Coordinator.Pause();
        h.Coordinator.PolicyPause();
        h.Coordinator.PolicyResume();
        Assert.Equal(PlaybackStatus.Paused, h.Coordinator.Status);
    }

    [Fact]
    public async Task 빈_플레이리스트_재생은_거부된다()
    {
        var h = new Harness(itemCount: 0);

        var result = await h.Coordinator.StartAsync(h.Playlist.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidInput, result.Code);
    }

    [Fact]
    public async Task 프로세스_실패_오류는_해당_플레이어만_재생성한다()
    {
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);
        var oldSlave = h.Players["MON-1"];
        h.Master.CurrentTime = 42.0;

        oldSlave.RaiseError(-2);
        await Task.Delay(50); // fire-and-forget 재생성 대기

        Assert.Contains("dispose", oldSlave.Commands);
        var newSlave = h.Players["MON-1"];
        Assert.NotSame(oldSlave, newSlave);
        Assert.Contains("load:video00000a@42.0", newSlave.Commands); // 현재 곡을 마스터 시각부터 이어서 (seek는 load에 통합)
    }

    [Fact]
    public async Task 일시정지_중_재생성된_플레이어는_재생하지_않고_일시정지_상태를_따른다()
    {
        // loadVideoById는 즉시 재생을 시작하므로, 일시정지 중 합류·재생성한 플레이어를 그대로 두면
        // 그 모니터만 재생된다(오디오 대상이면 소리까지) — ResumeCurrentTrack이 Paused를 따라야 한다 (리뷰 지적).
        var h = new Harness();
        await h.Coordinator.StartAsync(h.Playlist.Id);
        h.Master.CurrentTime = 42.0;
        h.Coordinator.Pause();
        var oldSlave = h.Players["MON-1"];

        oldSlave.RaiseError(-2);
        await Task.Delay(50); // fire-and-forget 재생성 대기

        var newSlave = h.Players["MON-1"];
        Assert.NotSame(oldSlave, newSlave);
        Assert.Contains("load:video00000a@42.0", newSlave.Commands); // 마스터 시각으로 이어서
        Assert.Contains("pause", newSlave.Commands); // 재생하지 않고 일시정지 상태를 따름
    }

    [Fact]
    public async Task 재생_시작_시_저장된_크기_모드를_모든_플레이어에_초기_적용한다()
    {
        var h = new Harness(monitorCount: 2);
        h.Settings.FitMode = FitMode.Contain;

        await h.Coordinator.StartAsync(h.Playlist.Id);

        // FR-16: 초기 적용은 StartAsync의 플레이어 생성 지점에서
        Assert.Contains("fit:1", h.Players["MON-0"].Commands);
        Assert.Contains("fit:1", h.Players["MON-1"].Commands);
    }

    [Fact]
    public async Task 미러_화질_하향을_켜면_소리_없는_모니터만_스케일이_제한된다()
    {
        var h = new Harness(monitorCount: 2);
        h.Settings.QualityScaleHeight = 1080;
        await h.Coordinator.StartAsync(h.Playlist.Id);

        await h.Coordinator.SetReduceMirrorQualityAsync(true);

        // D5: 마스터(오디오 대상)는 설정값 유지, 미러만 min(설정, 720)으로 하향
        Assert.Contains("scale:1080", h.Players["MON-0"].Commands);
        Assert.DoesNotContain("scale:720", h.Players["MON-0"].Commands);
        Assert.Contains("scale:720", h.Players["MON-1"].Commands);
        Assert.True(h.Settings.ReduceMirrorQuality);
    }

    [Fact]
    public async Task 미러_하향이_켜진_채_시작하면_원본_화질_미러는_720으로_제한된다()
    {
        var h = new Harness(monitorCount: 2);
        h.Settings.ReduceMirrorQuality = true; // 화질 0(원본) + 하향 켜짐 — StartAsync 초기 적용 경로

        await h.Coordinator.StartAsync(h.Playlist.Id);

        Assert.Contains("scale:0", h.Players["MON-0"].Commands);   // 마스터는 원본 유지
        Assert.Contains("scale:720", h.Players["MON-1"].Commands); // 미러는 720 상한
    }

    [Fact]
    public async Task 크기_모드_변경은_모든_플레이어에_전송되고_설정에_저장된다()
    {
        var h = new Harness(monitorCount: 2);
        await h.Coordinator.StartAsync(h.Playlist.Id);

        await h.Coordinator.SetFitModeAsync(FitMode.Stretch);

        // FR-16: 재생 중 변경 즉시 반영 (acceptance — fit 명령 전송 코드 경로)
        Assert.Equal(FitMode.Stretch, h.Settings.FitMode);
        Assert.Contains("fit:2", h.Players["MON-0"].Commands);
        Assert.Contains("fit:2", h.Players["MON-1"].Commands);
    }

    [Fact]
    public async Task 재생_시작_시_자막_설정이_모든_플레이어에_초기_적용된다()
    {
        var h = new Harness(monitorCount: 2); // 기본값 끔 (FR-20)

        await h.Coordinator.StartAsync(h.Playlist.Id);

        // FR-20: 켬/끔 어느 상태든 항상 명시 전송 (계정 자막 선호를 덮어야 하므로)
        Assert.Contains("captions:False", h.Players["MON-0"].Commands);
        Assert.Contains("captions:False", h.Players["MON-1"].Commands);
    }

    [Fact]
    public async Task 자막_변경은_모든_플레이어에_전송되고_설정에_저장된다()
    {
        var h = new Harness(monitorCount: 2);
        await h.Coordinator.StartAsync(h.Playlist.Id);

        await h.Coordinator.SetCaptionsEnabledAsync(true);

        // FR-20: 재생 중 변경 즉시 반영 + 설정 반영 (저장은 in-memory Settings로 확인 — FakeStore는 기록 없음)
        Assert.True(h.Settings.CaptionsEnabled);
        Assert.Contains("captions:True", h.Players["MON-0"].Commands);
        Assert.Contains("captions:True", h.Players["MON-1"].Commands);
    }

    [Fact]
    public async Task 전_곡_재생_불가_정지는_AllItemsFailed_이벤트를_발화한다()
    {
        var h = new Harness(itemCount: 2);
        var raised = 0;
        h.Coordinator.AllItemsFailed += (_, _) => raised++;
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Master.RaiseError(150); // 1곡째 → 2곡째 스킵
        h.Master.RaiseError(150); // 2곡째 — 서로 다른 2개 = 전 항목 재생 불가

        // 표시(창 표시·토스트)는 UI 계층 구독자 몫 — 코디네이터는 이벤트만 발화
        Assert.Equal(PlaybackStatus.Stopped, h.Coordinator.Status);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task 한곡반복_모드에서_재생_불가_곡은_반복하지_않고_정지한다()
    {
        var h = new Harness(itemCount: 3);
        h.Settings.Mode = PlaybackMode.RepeatOne;
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Master.RaiseError(150);

        // 다음 곡도 같은 곡이라 재시도가 무의미 — Count회 재로드하던 낭비 제거 (2026-07-17 수정)
        Assert.Equal(PlaybackStatus.Stopped, h.Coordinator.Status);
        Assert.Single(h.Master.Commands, c => c == "load:video00000a");
    }

    [Fact]
    public async Task 일시정지_중_재생_불가_오류는_즉시_전진하지_않고_재개_시_스킵한다()
    {
        var h = new Harness(itemCount: 3);
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Coordinator.Pause();
        h.Master.RaiseError(150); // 일시정지 직후 도착한 재생 불가 에러

        // 일시정지 중에는 전진하지 않는다 — loadVideoById가 즉시 재생을 시작해 일시정지를 깨기 때문
        Assert.Equal(PlaybackStatus.Paused, h.Coordinator.Status);
        Assert.DoesNotContain("load:video00001a", h.Master.Commands);

        h.Coordinator.Resume();

        // 재개 시 에러 난 곡 대신 다음 곡으로 스킵
        Assert.Equal(PlaybackStatus.Playing, h.Coordinator.Status);
        Assert.Contains("load:video00001a", h.Master.Commands);
    }

    [Fact]
    public async Task 마지막_재생_기록이_없으면_StartLastAsync는_NotFound를_반환한다()
    {
        var h = new Harness();

        var result = await h.Coordinator.StartLastAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.Code);
        Assert.Equal(PlaybackStatus.Stopped, h.Coordinator.Status);
    }

    [Fact]
    public async Task StartLastAsync는_마지막_리스트를_마지막_항목부터_재개한다()
    {
        var h = new Harness(itemCount: 3);
        h.Settings.LastPlaylistId = h.Playlist.Id;
        h.Settings.LastItemId = h.Playlist.Items[1].Id;

        var result = await h.Coordinator.StartLastAsync();

        // FR-19: 자동 시작·트레이 재생 공용 재개 경로 — 마지막 항목부터 시작
        Assert.True(result.IsSuccess);
        Assert.Contains("load:video00001a", h.Master.Commands);
    }

    [Fact]
    public async Task 재생_진행_확인_시_재생시간이_수집되어_모델_갱신_이벤트_저장된다()
    {
        var h = new Harness(itemCount: 2);
        Guid? capturedId = null;
        h.Coordinator.ItemDurationCaptured += (_, id) => capturedId = id;
        await h.Coordinator.StartAsync(h.Playlist.Id);
        var savesBefore = h.LibraryStore.PlaylistSaveCount;

        // FR-18: 실제 진행(≥1s) 시점에 duration이 함께 보고되면 현재 곡에 캐시
        h.Master.RaiseTime(5.0, duration: 204);

        Assert.Equal(204, h.Playlist.Items[0].DurationSeconds); // 모델(영속 대상) 갱신
        Assert.Equal(h.Playlist.Items[0].Id, capturedId);       // 수집 항목 ID로 이벤트 발화
        Assert.Equal(savesBefore + 1, h.LibraryStore.PlaylistSaveCount); // 저장 1회
    }

    [Fact]
    public async Task 재생시간이_0이면_수집하지_않는다()
    {
        var h = new Harness(itemCount: 2);
        var raised = 0;
        h.Coordinator.ItemDurationCaptured += (_, _) => raised++;
        await h.Coordinator.StartAsync(h.Playlist.Id);
        var savesBefore = h.LibraryStore.PlaylistSaveCount;

        // 라이브·미로드는 getDuration()이 0 — CurrentDuration 0 유지, 수집 차단 (FR-18)
        h.Master.RaiseTime(5.0);

        Assert.Equal(0, h.Playlist.Items[0].DurationSeconds); // 미수집
        Assert.Equal(0, raised);
        Assert.Equal(savesBefore, h.LibraryStore.PlaylistSaveCount); // 저장 없음
    }

    [Fact]
    public async Task 동일한_재생시간_재보고는_재저장_재발화하지_않는다()
    {
        var h = new Harness(itemCount: 2);
        var raised = 0;
        h.Coordinator.ItemDurationCaptured += (_, _) => raised++;
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Master.RaiseTime(5.0, duration: 204); // 최초 수집
        var savesAfterFirst = h.LibraryStore.PlaylistSaveCount;
        h.Master.RaiseTime(6.0, duration: 204); // 같은 값(반올림 차 <2s) 재보고 — 억제 (D8)

        Assert.Equal(1, raised);
        Assert.Equal(savesAfterFirst, h.LibraryStore.PlaylistSaveCount); // 추가 저장 없음
    }
}
