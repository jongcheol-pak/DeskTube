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

        public event EventHandler? Ready;
        public event EventHandler<PlayerState>? StateChanged;
        public event EventHandler<PlayerError>? ErrorOccurred;
        public event EventHandler<double>? TimeUpdated;

        public Task<Result> InitializeAsync() => Task.FromResult(Result.Ok());
        public void Load(string videoId) => Commands.Add($"load:{videoId}");
        public void Play() => Commands.Add("play");
        public void Pause() => Commands.Add("pause");
        public void SetVolume(int volume) => Commands.Add($"volume:{volume}");
        public void SetMuted(bool muted) => Commands.Add($"mute:{muted}");
        public void Seek(double seconds) => Commands.Add($"seek:{seconds:F1}");
        public void SetQualityScale(int height) => Commands.Add($"scale:{height}");
        public void SetFitMode(FitMode mode) => Commands.Add($"fit:{(int)mode}");
        public void Dispose() => Commands.Add("dispose");

        public void RaiseState(PlayerState state) => StateChanged?.Invoke(this, state);
        public void RaiseError(int code) => ErrorOccurred?.Invoke(this, new PlayerError(code));
        public void RaiseTime(double time) => TimeUpdated?.Invoke(this, time);
        public void RaiseReady() => Ready?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeStore : IStateStore
    {
        public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(new AppSettings());
        public Task<Result> SaveSettingsAsync(AppSettings settings) => Task.FromResult(Result.Ok());
        public Task<List<Playlist>> LoadPlaylistsAsync() => Task.FromResult(new List<Playlist>());
        public Task<Result> SavePlaylistsAsync(List<Playlist> playlists) => Task.FromResult(Result.Ok());
    }

    // ---- 테스트 하니스 ----

    private sealed class Harness
    {
        public FakeMonitorService Monitors { get; } = new();
        public FakeWallpaperHost Wallpaper { get; } = new();
        public Dictionary<string, FakePlayer> Players { get; } = [];
        public HashSet<string> FailPlayerMonitorIds { get; } = [];
        public PlaylistLibrary Library { get; } = new(new FakeStore());
        public AppSettings Settings { get; } = new();
        public PlaybackCoordinator Coordinator { get; }
        public Playlist Playlist { get; }

        public Harness(int monitorCount = 2, int itemCount = 3)
        {
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

            Coordinator = new PlaybackCoordinator(
                Monitors, Wallpaper, CreatePlayerAsync, Library, new FakeStore(), Settings);
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
    public async Task 순차_마지막_곡_종료_시_정지하고_배경을_복구한다()
    {
        var h = new Harness(itemCount: 1);
        await h.Coordinator.StartAsync(h.Playlist.Id);

        h.Master.RaiseState(PlayerState.Playing);
        h.Master.RaiseState(PlayerState.Ended);

        Assert.Equal(PlaybackStatus.Stopped, h.Coordinator.Status);
        Assert.Contains("detach-all", h.Wallpaper.Log);
        Assert.Contains("dispose", h.Players["MON-0"].Commands);
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
        Assert.Contains("load:video00000a", newSlave.Commands); // 현재 곡 이어서
        Assert.Contains("seek:42.0", newSlave.Commands); // 마스터 시각으로 동기
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
}
