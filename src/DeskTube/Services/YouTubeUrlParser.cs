using System.Text.RegularExpressions;
using DeskTube.Models;

namespace DeskTube.Services;

/// <summary>
/// 유튜브 URL → 영상 ID 파싱 (PRD FR-1). 형식 파싱만 수행하며 네트워크 접근 없음 (plan T3 Design ④).
/// 지원 형식: watch?v= / youtu.be/ / shorts/ / embed/ (+ live/), 쿼리 파라미터는 무시.
/// </summary>
public static partial class YouTubeUrlParser
{
    /// <summary>유튜브 영상 ID는 11자 [A-Za-z0-9_-] (공식 문서엔 없지만 사실상 표준 형식).</summary>
    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex VideoIdRegex();

    public static Result<string> Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<string>.Fail(ErrorCode.InvalidInput, "주소가 비어 있습니다.");
        }

        var text = input.Trim();

        // 스킴 생략 입력("youtube.com/...") 허용 — Uri 파싱을 위해 https를 보충
        if (!text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return Result<string>.Fail(ErrorCode.InvalidInput, "주소 형식을 인식할 수 없습니다.");
        }

        var host = uri.Host.ToLowerInvariant();
        string? candidate = null;

        if (host == "youtu.be")
        {
            // https://youtu.be/{id}
            candidate = FirstSegment(uri);
        }
        else if (host == "youtube.com" || host.EndsWith(".youtube.com", StringComparison.Ordinal))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 1 && segments[0].Equals("watch", StringComparison.OrdinalIgnoreCase))
            {
                // https://www.youtube.com/watch?v={id}
                candidate = ParseQueryValue(uri.Query, "v");
            }
            else if (segments.Length >= 2 &&
                     (segments[0].Equals("shorts", StringComparison.OrdinalIgnoreCase) ||
                      segments[0].Equals("embed", StringComparison.OrdinalIgnoreCase) ||
                      segments[0].Equals("live", StringComparison.OrdinalIgnoreCase)))
            {
                // https://www.youtube.com/shorts/{id}, /embed/{id}, /live/{id}
                candidate = segments[1];
            }
        }
        else
        {
            return Result<string>.Fail(ErrorCode.InvalidInput, "유튜브 주소가 아닙니다.");
        }

        return candidate is not null && VideoIdRegex().IsMatch(candidate)
            ? Result<string>.Ok(candidate)
            : Result<string>.Fail(ErrorCode.InvalidInput, "주소에서 영상 ID를 찾을 수 없습니다.");
    }

    private static string? FirstSegment(Uri uri)
    {
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 1 ? segments[0] : null;
    }

    /// <summary>쿼리 문자열에서 지정 키의 값을 찾는다 (System.Web 의존 없이 최소 구현).</summary>
    private static string? ParseQueryValue(string query, string key)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq > 0 && pair[..eq].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        }

        return null;
    }
}
