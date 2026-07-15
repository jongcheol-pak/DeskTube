namespace DeskTube.Services;

/// <summary>
/// 파일 로그 (plan D11) — ApplicationData\logs\desktube-{날짜}.log.
/// 로깅 실패가 앱을 죽이면 안 되므로 IO 예외는 삼킨다. 개인정보·계정정보는 기록 금지.
/// </summary>
public static class AppLog
{
    private static readonly object Sync = new();
    private static string? _logDirectory;

    /// <summary>로그 폴더 지정 (앱 시작 시 1회). 미지정 상태의 Write는 무시된다.</summary>
    public static void Initialize(string logDirectory)
    {
        lock (Sync)
        {
            _logDirectory = logDirectory;
        }
    }

    public static void Write(string message)
    {
        lock (Sync)
        {
            if (_logDirectory is null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(_logDirectory);
                var path = Path.Combine(_logDirectory, $"desktube-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // 로그 기록 실패는 무시 (디스크 풀 등 — 앱 동작에 영향 주지 않음)
            }
            catch (UnauthorizedAccessException)
            {
                // 권한 문제도 동일하게 무시
            }
        }
    }
}
