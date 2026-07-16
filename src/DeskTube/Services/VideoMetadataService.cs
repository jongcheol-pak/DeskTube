using System.Text.Json;
using DeskTube.Models;

namespace DeskTube.Services;

/// <summary>유튜브 영상 메타데이터 (FR-18) — oEmbed가 제공하는 표시용 2필드.</summary>
public sealed record VideoMetadata(string Title, string ChannelName);

/// <summary>
/// 유튜브 oEmbed로 영상 제목·채널명을 조회한다 (FR-18, plan T2).
/// API 키·로그인 쿠키 불필요, 영상 ID 외 어떤 정보도 전송하지 않는다 (PRD NFR-6 상충 없음).
/// 실패는 Result로 반환하고 예외를 전파하지 않는다 — 호출측은 URL 표시로 폴백.
/// </summary>
public sealed class VideoMetadataService
{
    /// <summary>정적 단일 인스턴스 — 요청마다 생성 시 소켓 고갈 위험 (.NET 권장 사용법, plan D11).</summary>
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    /// <summary>영상 ID로 제목·채널명을 조회한다. 네트워크·응답 오류는 Result 실패로 흡수한다.</summary>
    public async Task<Result<VideoMetadata>> FetchAsync(string videoId, CancellationToken cancellationToken = default)
    {
        // 사용자 입력 원본 URL 대신 정규 watch URL을 구성 — youtu.be/shorts 등 변형과 무관하게 동일 결과
        var watchUrl = Uri.EscapeDataString($"https://www.youtube.com/watch?v={videoId}");
        var requestUri = $"https://www.youtube.com/oembed?url={watchUrl}&format=json";

        try
        {
            using var response = await Http.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // 404/401 = 삭제·비공개 영상 — 조용히 폴백 (재시도 무의미)
                return Result<VideoMetadata>.Fail(ErrorCode.NotFound, $"oEmbed 응답 {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return TryParse(json, out var metadata)
                ? Result<VideoMetadata>.Ok(metadata)
                : Result<VideoMetadata>.Fail(ErrorCode.InvalidInput, "oEmbed 응답 파싱 실패");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // 호출측 취소(페이지 이탈·리스트 전환)는 오류가 아니므로 그대로 전파
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            // TaskCanceledException(취소 토큰 미발동) = HttpClient 타임아웃
            return Result<VideoMetadata>.Fail(ErrorCode.EnvironmentFailure, ex.GetType().Name);
        }
    }

    /// <summary>
    /// oEmbed JSON에서 title·author_name을 추출한다. 순수 함수 — 네트워크 없이 단위 테스트 (plan D11).
    /// 필드 결손·비정상 JSON이면 false (예외 없음).
    /// </summary>
    public static bool TryParse(string json, out VideoMetadata metadata)
    {
        metadata = new VideoMetadata(string.Empty, string.Empty);
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("title", out var title) ||
                title.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            // 채널명은 없어도 제목만으로 유효 (표시 필수 항목은 제목)
            var channel = document.RootElement.TryGetProperty("author_name", out var author) &&
                          author.ValueKind == JsonValueKind.String
                ? author.GetString()!
                : string.Empty;

            metadata = new VideoMetadata(title.GetString()!, channel);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
