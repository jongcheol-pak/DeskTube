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

    private readonly WallpaperSurface _surface;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
    private CoreWebView2Controller? _controller;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _retryTimer;
    private int _apiLoadRetries;
    private bool _renderProcessRecovered;

    /// <param name="dispatcherQueue">재시도 타이머용 UI 디스패처 (Win32 창엔 DispatcherQueue가 없음 — plan D4)</param>
    internal PlayerHost(WallpaperSurface surface, Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue)
    {
        _surface = surface;
        _dispatcherQueue = dispatcherQueue;
    }

    public event EventHandler? Ready;
    public event EventHandler<PlayerState>? StateChanged;
    public event EventHandler<PlayerError>? ErrorOccurred;
    public event EventHandler<double>? TimeUpdated;

    public double CurrentTime { get; private set; }

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

            var core = _controller.CoreWebView2;

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

            core.Navigate($"https://{VirtualHost}/player.html");
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

    public void Load(string videoId) => PostCommand(new PlayerCommand("load", VideoId: videoId));

    public void Play() => PostCommand(new PlayerCommand("play"));

    public void Pause() => PostCommand(new PlayerCommand("pause"));

    public void SetVolume(int volume) => PostCommand(new PlayerCommand("volume", Volume: Math.Clamp(volume, 0, 100)));

    public void SetMuted(bool muted) => PostCommand(new PlayerCommand("mute", Muted: muted));

    public void Seek(double seconds) => PostCommand(new PlayerCommand("seek", Seconds: seconds));

    public void SetQualityScale(int height) => PostCommand(new PlayerCommand("scale", Height: height));

    public void Dispose()
    {
        _retryTimer?.Stop();
        _retryTimer = null;
        _surface.Resized -= OnSurfaceResized;

        if (_controller?.CoreWebView2 is { } core)
        {
            core.WebMessageReceived -= OnWebMessageReceived;
            core.ProcessFailed -= OnProcessFailed;
        }

        _controller?.Close();
        _controller = null;
    }

    /// <summary>배경창 크기 변경 추종 (해상도 변경 재배치 — plan D3).</summary>
    private void OnSurfaceResized(int width, int height) =>
        _controller?.Bounds = new Windows.Foundation.Rect(0, 0, width, height);

    private void PostCommand(PlayerCommand command)
    {
        // 초기화 전·파괴 후 명령은 무시 (ready 전 큐잉은 JS 측이 담당)
        if (_controller?.CoreWebView2 is { } core)
        {
            core.PostWebMessageAsJson(JsonSerializer.Serialize(command, PlayerJsonContext.Default.PlayerCommand));
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
                case "ready":
                    _apiLoadRetries = 0;
                    Ready?.Invoke(this, EventArgs.Empty);
                    break;

                case "state":
                    StateChanged?.Invoke(this, (PlayerState)root.GetProperty("state").GetInt32());
                    break;

                case "error":
                    HandlePlayerError(root.GetProperty("code").GetInt32());
                    break;

                case "time":
                    CurrentTime = root.GetProperty("current").GetDouble();
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
            _retryTimer.Tick += (_, _) => _controller?.CoreWebView2?.Reload();
            _retryTimer.Start();
            return;
        }

        var error = new PlayerError(code);
        // acceptance: 임베드 금지(101/150) 등 오류 수신은 로그로 확인 가능해야 한다 (T6)
        AppLog.Write($"플레이어 오류 수신: 코드 {code}{(error.IsEmbedForbidden ? " (임베드 금지 영상)" : "")}");
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
            _controller?.CoreWebView2?.Reload();
            return;
        }

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
    bool? Muted = null);

/// <summary>플레이어 명령 직렬화 컨텍스트 (리플렉션 회피 — D5와 동일 방침).</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PlayerCommand))]
internal sealed partial class PlayerJsonContext : JsonSerializerContext;
