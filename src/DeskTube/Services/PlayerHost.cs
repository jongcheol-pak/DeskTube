using System.Text.Json;
using System.Text.Json.Serialization;
using DeskTube.Interop;
using DeskTube.Models;
using Microsoft.Web.WebView2.Core;

namespace DeskTube.Services;

/// <summary>
/// WebView2 기반 유튜브 플레이어 (PRD FR-1·5·13, plan D8·D9).
/// Win32 배경 표면(WallpaperSurface)의 HWND에 CoreWebView2Controller를 호스팅하고
/// player.html과 postMessage로 통신한다 — XAML WebView2 컨트롤은 크로스 프로세스 재부모화
/// 창에서 AV로 죽어 컨트롤러 호스팅으로 전환 (plan 2026-07-16 D3).
/// UI 스레드에서만 호출하는 전제 (plan D10).
/// </summary>
public sealed class PlayerHost : IPlayerHost
{
    /// <summary>로컬 player.html을 https 오리진으로 서빙하기 위한 가상 호스트 (IFrame API의 file:// 제약 회피).</summary>
    private const string VirtualHost = "player.desktube.local";

    private const int MaxApiLoadRetries = 3;
    private static readonly TimeSpan ApiRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>player.html의 message 리스너 준비(hostReady) 대기 상한 — 초과 시 경고 후 진행(기존 동작 폴백).</summary>
    private static readonly TimeSpan HostReadyTimeout = TimeSpan.FromSeconds(10);

    private readonly WallpaperSurface _surface;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
    private CoreWebView2Controller? _controller;

    /// <summary>CoreWebView2 RCW 강참조 — 반드시 필드로 수명을 고정해야 한다 (2026-07-18 크래시 수정).
    /// CsWinRT의 WinRT 이벤트 구독 상태는 ConditionalWeakTable&lt;RCW, EventSource&gt;로 RCW 인스턴스에
    /// 약하게 붙으므로, 프로퍼티(_controller.CoreWebView2)로만 접근하면 GC가 RCW를 수집해
    /// (페이지 전환 등 할당 급증 시) 네이티브 WebMessageReceived 콜백이 해제된 스텁을 호출
    /// → CLR fatal(ExecutionEngineException)로 즉사한다 — 덤프 분석으로 확정된 재생 중 크래시.</summary>
    private CoreWebView2? _core;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _retryTimer;
    private int _apiLoadRetries;
    private bool _renderProcessRecovered;

    /// <summary>브라우저 프로세스 소멸 감지용 — 환경은 앱 전역 공유(캐시)라 이벤트 구독 해제에 참조가 필요하다.</summary>
    private CoreWebView2Environment? _environment;

    /// <summary>player.html의 message 리스너 준비 신호(hostReady) 대기용. 초기화가 이 신호까지 기다린 뒤
    /// 완료를 반환해, 이후 전송되는 명령이 리스너 등록 전 폐기되는 것을 막는다 (2026-07-19 멀티모니터 미재생 수정).</summary>
    private readonly TaskCompletionSource _hostReadyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>이 플레이어가 붙은 브라우저 프로세스 PID — BrowserProcessExited가 이전 브라우저의
    /// 정상 종료(정지→재시작 직후 새 플레이어가 받는 이벤트)인지 내 브라우저의 소멸인지 구분한다.</summary>
    private uint _browserPid;

    private bool _disposed;

    /// <param name="dispatcherQueue">재시도 타이머용 UI 디스패처 (Win32 창엔 DispatcherQueue가 없음 — plan D4)</param>
    internal PlayerHost(WallpaperSurface surface, Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue)
    {
        _surface = surface;
        _dispatcherQueue = dispatcherQueue;
    }

    /// <summary>플레이어 인스턴스 식별 태그 — 배경창 hwnd 하위 4자리로 멀티모니터의 여러 플레이어를
    /// 로그에서 구분한다 (어느 모니터의 이벤트인지 판독용).</summary>
    private string Tag => $"P{_surface.Hwnd.ToInt64() & 0xFFFF:X4}";

    public event EventHandler? Ready;
    public event EventHandler<PlayerState>? StateChanged;
    public event EventHandler<PlayerError>? ErrorOccurred;
    public event EventHandler<double>? TimeUpdated;

    public double CurrentTime { get; private set; }

    public double CurrentDuration { get; private set; }

    public async Task<Result> InitializeAsync()
    {
        try
        {
            var environment = await WebViewEnvironment.GetAsync();
            var windowRef = CoreWebView2ControllerWindowReference.CreateFromWindowHandle((ulong)_surface.Hwnd);
            _controller = await environment.CreateCoreWebView2ControllerAsync(windowRef);

            // Win32 호스팅은 host가 Bounds를 직접 관리한다 (자동 리사이즈 없음 — plan D3)
            var (width, height) = _surface.ClientSize;
            _controller.Bounds = new Windows.Foundation.Rect(0, 0, width, height);
            _controller.IsVisible = true;
            _surface.Resized += OnSurfaceResized;

            var core = _core = _controller.CoreWebView2; // RCW 강참조 확보 — 필드 주석 참조

            // 배경화면 용도 — 사용자 상호작용 UI 전부 차단
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;

            core.SetVirtualHostNameToFolderMapping(
                VirtualHost,
                Path.Combine(AppContext.BaseDirectory, "Assets"),
                CoreWebView2HostResourceAccessKind.Allow);

            core.WebMessageReceived += OnWebMessageReceived;
            core.ProcessFailed += OnProcessFailed;

            // 브라우저 프로세스 소멸 안전망 (2026-07-18 조사) — 정지→즉시 재시작 시 웹뷰 수가 0이 되는
            // 순간 브라우저가 종료 수순에 들어가고, 그 사이 만든 새 컨트롤러가 죽어가는 브라우저에 붙으면
            // ProcessFailed조차 없이 전 프로세스가 소멸한다(Buffering에서 상태·JS 타이머까지 침묵).
            // 환경 이벤트로 소멸을 감지해 코디네이터 재생성(-2)으로 복구한다. PID 기록은 진단 로그 겸
            // 이벤트 필터 기준.
            _environment = environment;
            _browserPid = core.BrowserProcessId;
            environment.BrowserProcessExited += OnBrowserProcessExited;

            // 쿼리스트링 캐시 무력화 — WebView2가 가상 호스트 응답을 HTTP 캐시할 수 있어(프로필 영속),
            // 앱 업데이트 후에도 옛 player.html이 실행되는 것을 방지한다 (실행마다 새 URL → 항상 디스크 최신본)
            core.Navigate($"https://{VirtualHost}/player.html?t={Environment.TickCount64}");

            // player.html이 message 리스너를 등록하고 hostReady를 보낼 때까지 대기한 뒤 완료를 반환한다.
            // 이 대기가 없으면 초기화 직후 코디네이터가 보낸 load 등이 페이지 로딩 공백 중 폐기되어,
            // 멀티모니터 동시 시작 시 일부 플레이어가 재생되지 않았다 (2026-07-19 수정).
            var ready = await Task.WhenAny(_hostReadyTcs.Task, Task.Delay(HostReadyTimeout));
            if (ready != _hostReadyTcs.Task)
            {
                // 시간 초과 — 명령 유실 가능성을 감수하고 진행(기존 동작 폴백). 실패로 막지 않는다.
                AppLog.Write($"[{Tag}] player.html 준비 신호(hostReady) 시간 초과 — 진행");
            }

            AppLog.Write($"[{Tag}] 플레이어 초기화 완료 (브라우저 PID {_browserPid})");
            return Result.Ok();
        }
        catch (Exception ex)
        {
            // WebView2 런타임 부재는 별도 안내 (Win11은 기본 탑재라 드묾 — plan Known Workarounds).
            // 전용 예외 타입이 WinRT 프로젝션에 없어 HResult(ERROR_FILE_NOT_FOUND)로 판별한다.
            var runtimeMissing = ex is DllNotFoundException || ex.HResult == unchecked((int)0x80070002);
            AppLog.Write($"플레이어 초기화 실패: {ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}");
            return Result.Fail(
                ErrorCode.EnvironmentFailure,
                runtimeMissing
                    ? "동영상 표시 구성 요소(WebView2)를 사용할 수 없습니다. Windows 업데이트 후 다시 시도해 주세요."
                    : "동영상 플레이어를 준비하지 못했습니다.");
        }
    }

    public void Load(string videoId, double startSeconds = 0)
    {
        // 진단 로그 — 어떤 영상이 로드되는지 로그로 추적 (2026-07-18 "안 됨" 조사: 영상 식별 불가 문제)
        AppLog.Write($"[{Tag}] 플레이어 명령: load {videoId}{(startSeconds > 0 ? $" @{startSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s" : "")}");
        // startSeconds > 0만 실어 보낸다 — 0은 Seconds 미포함(WhenWritingNull)으로 직렬화돼 JS가 0으로 처리.
        PostCommand(new PlayerCommand("load", VideoId: videoId, Seconds: startSeconds > 0 ? startSeconds : null));
    }

    public void Play()
    {
        AppLog.Write("플레이어 명령: play");
        PostCommand(new PlayerCommand("play"));
    }

    public void Pause()
    {
        AppLog.Write("플레이어 명령: pause"); // pause는 시작 감시(startWatchdog)도 취소하므로 시점 기록이 중요
        PostCommand(new PlayerCommand("pause"));
    }

    public void SetVolume(int volume) => PostCommand(new PlayerCommand("volume", Volume: Math.Clamp(volume, 0, 100)));

    public void SetMuted(bool muted) => PostCommand(new PlayerCommand("mute", Muted: muted));

    public void Seek(double seconds) => PostCommand(new PlayerCommand("seek", Seconds: seconds));

    public void SetQualityScale(int height) => PostCommand(new PlayerCommand("scale", Height: height));

    public void SetFitMode(FitMode mode) => PostCommand(new PlayerCommand("fit", Mode: (int)mode));

    public void SetCaptionsEnabled(bool enabled) => PostCommand(new PlayerCommand("captions", Enabled: enabled));

    /// <summary>절전 — 렌더링 중단(IsVisible=false) 후 렌더러 절전 요청.
    /// TrySuspendAsync는 IsVisible=false 선행이 필수이며 best-effort다(거부돼도 렌더링 중단 효과는 유지 — plan D7).</summary>
    public void Suspend()
    {
        if (_controller is not { } controller || _core is not { } core)
        {
            return;
        }

        // IsVisible 설정도 프로세스 무효 창에서 던질 수 있음 — PostCommand와 동일 부류라 함께 흡수
        TryInteract("플레이어 절전", () =>
        {
            controller.IsVisible = false;
            _ = TrySuspendCoreAsync(core);
        });
    }

    private static async Task TrySuspendCoreAsync(CoreWebView2 core)
    {
        try
        {
            var suspended = await core.TrySuspendAsync();
            if (!suspended)
            {
                AppLog.Write("플레이어 절전 요청 거부(렌더링 중단만 적용) — best-effort");
            }
        }
        catch (Exception ex)
        {
            // 절전 실패는 재생 동작에 영향 없음 — 기록만 (plan D7)
            AppLog.Write($"플레이어 절전 요청 실패(렌더링 중단만 적용): {ex.GetType().Name}");
        }
    }

    /// <summary>절전 해제 — 명시 Resume 후 가시화 (가시화만으로도 자동 재개되지만 순서를 못박아 결정적으로).</summary>
    public void ResumeFromSuspend()
    {
        if (_controller is not { } controller)
        {
            return;
        }

        TryInteract("플레이어 절전 해제", () =>
        {
            if (_core is { IsSuspended: true } core)
            {
                core.Resume();
            }

            controller.IsVisible = true;
        });
    }

    public void Dispose()
    {
        _disposed = true;
        _retryTimer?.Stop();
        _retryTimer = null;
        _surface.Resized -= OnSurfaceResized;

        // 정상 폐기 경로 — 구독을 먼저 해제하므로 이후 브라우저의 정상 종료는 소멸 감지에 걸리지 않는다
        if (_environment is { } environment)
        {
            environment.BrowserProcessExited -= OnBrowserProcessExited;
        }

        _environment = null;

        if (_core is { } core)
        {
            core.WebMessageReceived -= OnWebMessageReceived;
            core.ProcessFailed -= OnProcessFailed;
        }

        _controller?.Close();
        _controller = null;
        _core = null; // RCW 강참조 해제 — 이후 GC 수집 허용 (구독은 위에서 해제됨)
    }

    /// <summary>배경창 크기 변경 추종 (해상도 변경 재배치 — plan D3).</summary>
    private void OnSurfaceResized(int width, int height) =>
        TryInteract("플레이어 크기 반영", () =>
            _controller?.Bounds = new Windows.Foundation.Rect(0, 0, width, height));

    private void PostCommand(PlayerCommand command)
    {
        // 초기화 전·파괴 후 명령은 무시 (ready 전 큐잉은 JS 측이 담당)
        if (_core is { } core)
        {
            // 프로세스 크래시 직후 core가 무효인 짧은 창에선 IsSuspended 조회부터 0x8007139F를 던진다
            // — 조회·전송 전체를 흡수 (복구는 OnProcessFailed 담당, 2026-07-18 정책 일시정지 크래시 수정)
            TryInteract("플레이어 명령 전송", () =>
            {
                // 절전 중엔 페이지 스크립트가 정지라 메시지 처리가 보장되지 않음 — 명시 해제 후 전송.
                // 재절전은 하지 않는다 (드문 경로 — 다음 정책 일시정지에서 회복, plan D3)
                if (core.IsSuspended)
                {
                    ResumeFromSuspend();
                }

                core.PostWebMessageAsJson(JsonSerializer.Serialize(command, PlayerJsonContext.Default.PlayerCommand));
            });
        }
    }

    /// <summary>
    /// WebView2 상호작용을 감싸 렌더러 크래시 직후(컨트롤은 살아있으나 코어가 무효인 짧은 창)의
    /// 예외를 흡수한다. 전용 예외 타입이 WinRT 프로젝션에 없어 초기화 경로와 동일하게 넓게 잡는다.
    /// 명령 전송·크기 반영·절전 해제는 best-effort — ProcessFailed 복구나 다음 명령에서 회복된다.
    /// </summary>
    private static void TryInteract(string context, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            AppLog.Write($"{context} 실패(무시): {ex.GetType().Name} 0x{ex.HResult:X8}");
        }
    }

    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case "hostReady":
                    // player.html의 message 리스너 준비 완료 — InitializeAsync의 대기를 해제한다.
                    // 이 신호 이후 전송되는 명령은 리스너(및 YT ready 전 pending 큐)에 확실히 도달한다.
                    _hostReadyTcs.TrySetResult();
                    break;

                case "ready":
                    _apiLoadRetries = 0;
                    // rev 로그 — 실행 중인 player.html 버전 확인 (WebView2 캐시로 옛 파일이 돌 수 있음)
                    AppLog.Write($"[{Tag}] 플레이어 준비 (player.html rev {(root.TryGetProperty("rev", out var rev) ? rev.GetInt32() : 0)})");
                    Ready?.Invoke(this, EventArgs.Empty);
                    break;

                case "state":
                    var state = (PlayerState)root.GetProperty("state").GetInt32();
                    AppLog.Write($"[{Tag}] 플레이어 상태: {state}"); // 진단 로그 — 상태 전이 추적 (2026-07-18)
                    StateChanged?.Invoke(this, state);
                    break;

                case "error":
                    HandlePlayerError(root.GetProperty("code").GetInt32());
                    break;

                case "diag":
                    // JS 측 진단 보고 (시작 감시 판정 근거 등) — 스킵 미동작류 조사용
                    AppLog.Write($"[{Tag}] 플레이어 진단: {root.GetProperty("msg").GetString()}");
                    break;

                case "time":
                    CurrentTime = root.GetProperty("current").GetDouble();
                    // duration은 신 rev(6+)만 동반 — 옛 rev 캐시 실행 시 필드가 없으면 기존 값 유지 (FR-18)
                    if (root.TryGetProperty("duration", out var duration))
                    {
                        CurrentDuration = duration.GetDouble();
                    }

                    TimeUpdated?.Invoke(this, CurrentTime);
                    break;
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            AppLog.Write($"플레이어 메시지 파싱 실패(무시): {ex.GetType().Name}");
        }
    }

    private void HandlePlayerError(int code)
    {
        // -1 = IFrame API 로드 실패(네트워크) → 30초 후 재시도 최대 3회 (plan T6 Edge Case)
        if (code == -1 && _apiLoadRetries < MaxApiLoadRetries)
        {
            _apiLoadRetries++;
            AppLog.Write($"유튜브 API 로드 실패 — {ApiRetryDelay.TotalSeconds}초 후 재시도 ({_apiLoadRetries}/{MaxApiLoadRetries})");

            _retryTimer?.Stop();
            _retryTimer = _dispatcherQueue.CreateTimer();
            _retryTimer.Interval = ApiRetryDelay;
            _retryTimer.IsRepeating = false;
            _retryTimer.Tick += (_, _) => _core?.Reload();
            _retryTimer.Start();
            return;
        }

        var error = new PlayerError(code);
        // acceptance: 임베드 금지(101/150) 등 오류 수신은 로그로 확인 가능해야 한다 (T6)
        AppLog.Write($"[{Tag}] 플레이어 오류 수신: 코드 {code}{(error.IsEmbedForbidden ? " (임베드 금지 영상)" : "")}");
        ErrorOccurred?.Invoke(this, error);
    }

    private void OnProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs e)
    {
        AppLog.Write($"WebView2 프로세스 실패: {e.ProcessFailedKind}");

        // 렌더러 프로세스 크래시는 1회 Reload로 복구 시도 (plan T6 Edge Case),
        // 렌더러 외 모든 실패(브라우저·GPU 프로세스 등)는 컨트롤 재생성이 필요하므로 코디네이터에 위임(-2)
        if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited && !_renderProcessRecovered)
        {
            _renderProcessRecovered = true;
            _core?.Reload();
            return;
        }

        ErrorOccurred?.Invoke(this, new PlayerError(-2));
    }

    /// <summary>브라우저 프로세스 종료 감지 — 내 브라우저(PID 일치)가 살아있어야 할 때 소멸하면
    /// 컨트롤 재생성이 유일한 복구 수단이므로 -2로 위임한다 (ProcessFailed 미발화 케이스 보완).</summary>
    private void OnBrowserProcessExited(CoreWebView2Environment sender, CoreWebView2BrowserProcessExitedEventArgs e)
    {
        if (_disposed || e.BrowserProcessId != _browserPid)
        {
            return; // 폐기 후 잔여 이벤트·이전 브라우저의 정상 종료(정지→재시작 직후)는 무시
        }

        AppLog.Write($"[{Tag}] WebView2 브라우저 프로세스 소멸 감지 (kind={e.BrowserProcessExitKind}, PID {e.BrowserProcessId}) — 플레이어 재생성 요청");
        ErrorOccurred?.Invoke(this, new PlayerError(-2));
    }
}

/// <summary>C# → JS 명령 페이로드 (player.html의 cmd.Type/... 과 대소문자 일치).</summary>
public sealed record PlayerCommand(
    string Type,
    string? VideoId = null,
    int? Volume = null,
    double? Seconds = null,
    int? Height = null,
    bool? Muted = null,
    int? Mode = null,
    bool? Enabled = null);

/// <summary>플레이어 명령 직렬화 컨텍스트 (리플렉션 회피 — D5와 동일 방침).</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PlayerCommand))]
internal sealed partial class PlayerJsonContext : JsonSerializerContext;
