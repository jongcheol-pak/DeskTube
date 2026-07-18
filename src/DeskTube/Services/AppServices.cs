using DeskTube.Models;
using Microsoft.UI.Dispatching;

namespace DeskTube.Services;

/// <summary>
/// 컴포지션 루트 — 서비스 수동 배선 (plan T7 Design ③).
/// DI 컨테이너(Microsoft.Extensions.DependencyInjection)는 사전 승인 의존성 목록에 없어
/// part1에서는 도입하지 않는다 (part2 ViewModel 배선 시 재검토 — plan Deferred).
/// </summary>
public sealed class AppServices : IDisposable
{
    private AppServices(
        AppSettings settings,
        IStateStore store,
        PlaylistLibrary library,
        IMonitorService monitors,
        WallpaperHost wallpaperHost,
        PlaybackCoordinator coordinator,
        PowerPolicyService powerPolicy,
        VideoMetadataService metadata)
    {
        Settings = settings;
        Store = store;
        Library = library;
        Monitors = monitors;
        WallpaperHost = wallpaperHost;
        Coordinator = coordinator;
        PowerPolicy = powerPolicy;
        Metadata = metadata;
    }

    public AppSettings Settings { get; }

    public IStateStore Store { get; }

    public PlaylistLibrary Library { get; }

    public IMonitorService Monitors { get; }

    public WallpaperHost WallpaperHost { get; }

    public PlaybackCoordinator Coordinator { get; }

    public PowerPolicyService PowerPolicy { get; }

    /// <summary>유튜브 oEmbed 메타데이터 조회 (FR-18).</summary>
    public VideoMetadataService Metadata { get; }

    /// <summary>UI 스레드에서 호출 — 상태 로드 후 서비스 그래프를 조립한다.</summary>
    public static async Task<AppServices> CreateAsync(DispatcherQueue dispatcherQueue)
    {
        var dataDir = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        IStateStore store = new JsonStateStore(dataDir);
        var settings = await store.LoadSettingsAsync();

        var library = new PlaylistLibrary(store);
        await library.InitializeAsync();

        IMonitorService monitors = new MonitorService();
        var wallpaperHost = new WallpaperHost();

        // 플레이어 팩토리 — 배경 표면(Win32) 접근은 이 배선에서만 (plan T7 Design ②, 2026-07-16 전환)
        async Task<Result<IPlayerHost>> CreatePlayerAsync(MonitorInfo monitor)
        {
            var surface = wallpaperHost.GetSurface(monitor.Id);
            if (surface is null)
            {
                return Result<IPlayerHost>.Fail(ErrorCode.EnvironmentFailure, "배경창이 준비되지 않았습니다.");
            }

            var player = new PlayerHost(surface, dispatcherQueue);
            var initialized = await player.InitializeAsync();
            if (!initialized.IsSuccess)
            {
                player.Dispose();

                // 정지 직후 재시작 레이스 — 직전 브라우저 프로세스가 종료 수순 중이면 컨트롤러 생성이
                // 일시 실패(E_ABORT 등)한다. 종료가 끝나길 짧게 기다렸다 1회만 재시도한다 (2026-07-18 조사).
                // 런타임 부재 같은 영구 실패도 재시도 1회의 지연만 추가될 뿐 결과는 동일하다.
                AppLog.Write("플레이어 초기화 1회 재시도 (직전 브라우저 종료 대기)");
                await Task.Delay(TimeSpan.FromMilliseconds(1500));
                player = new PlayerHost(surface, dispatcherQueue);
                initialized = await player.InitializeAsync();
                if (!initialized.IsSuccess)
                {
                    player.Dispose();
                    return Result<IPlayerHost>.Fail(initialized.Code, initialized.Message);
                }
            }

            return Result<IPlayerHost>.Ok(player);
        }

        var coordinator = new PlaybackCoordinator(
            monitors,
            wallpaperHost,
            CreatePlayerAsync,
            library,
            store,
            settings,
            action => dispatcherQueue.TryEnqueue(() => action()));

        // 자동 일시정지 정책 연동 (NFR-1 — T8): 신호 합성 → 코디네이터 정책 일시정지/재개
        var powerPolicy = new PowerPolicyService(settings);
        powerPolicy.PauseRequested += (_, _) => coordinator.PolicyPause();
        powerPolicy.ResumeRequested += (_, _) => coordinator.PolicyResume();
        powerPolicy.StartMonitoring(dispatcherQueue);

        var metadata = new VideoMetadataService();

        return new AppServices(settings, store, library, monitors, wallpaperHost, coordinator, powerPolicy, metadata);
    }

    public void Dispose()
    {
        PowerPolicy.Dispose();
        Coordinator.Dispose();
        WallpaperHost.Dispose();
        Monitors.Dispose();
    }
}
