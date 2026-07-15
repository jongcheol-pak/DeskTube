using System.Text.Json;
using System.Text.Json.Serialization;
using DeskTube.Models;
using DeskTube.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace DeskTube.Services;

/// <summary>
/// WebView2 기반 유튜브 플레이어 (PRD FR-1·5·13, plan D8·D9).
/// WallpaperWindow 안의 WebView2 1개를 소유하고 player.html과 postMessage로 통신한다.
/// UI 스레드에서만 호출하는 전제 (plan D10).
/// </summary>
public sealed class PlayerHost : IPlayerHost
{
    /// <summary>로컬 player.html을 https 오리진으로 서빙하기 위한 가상 호스트 (IFrame API의 file:// 제약 회피).</summary>
    private const string VirtualHost = "player.desktube.local";

    private const int MaxApiLoadRetries = 3;
    private static readonly TimeSpan ApiRetryDelay = TimeSpan.FromSeconds(30);

    private readonly WallpaperWindow _window;
    private WebView2? _webView;
    private int _apiLoadRetries;
    private bool _renderProcessRecovered;

    public PlayerHost(WallpaperWindow window)
    {
        _window = window;
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
            _webView = new WebView2();
            _window.AttachContent(_webView);

            await _webView.EnsureCoreWebView2Async(environment);
            var core = _webView.CoreWebView2;

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
            AppLog.Write($"플레이어 초기화 실패: {ex.GetType().Name} {ex.Message}");
            return Result.Fail(ErrorCode.EnvironmentFailure, "동영상 플레이어를 준비하지 못했습니다.");
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
        if (_webView?.CoreWebView2 is { } core)
        {
            core.WebMessageReceived -= OnWebMessageReceived;
            core.ProcessFailed -= OnProcessFailed;
        }

        _webView?.Close();
        _webView = null;
    }

    private void PostCommand(PlayerCommand command)
    {
        // 초기화 전·파괴 후 명령은 무시 (ready 전 큐잉은 JS 측이 담당)
        if (_webView?.CoreWebView2 is { } core)
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

            var timer = _window.DispatcherQueue.CreateTimer();
            timer.Interval = ApiRetryDelay;
            timer.IsRepeating = false;
            timer.Tick += (_, _) => _webView?.CoreWebView2?.Reload();
            timer.Start();
            return;
        }

        ErrorOccurred?.Invoke(this, new PlayerError(code));
    }

    private void OnProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs e)
    {
        AppLog.Write($"WebView2 프로세스 실패: {e.ProcessFailedKind}");

        // 렌더러 프로세스 크래시는 1회 Reload로 복구 시도 (plan T6 Edge Case),
        // 브라우저 프로세스 종료는 컨트롤 재생성이 필요하므로 코디네이터에 위임(-2)
        if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited && !_renderProcessRecovered)
        {
            _renderProcessRecovered = true;
            _webView?.Reload();
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
