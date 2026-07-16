using DeskTube.Services;
using Xunit;

namespace DeskTube.Tests;

/// <summary>oEmbed 응답 파싱 검증 (FR-18, plan T2 acceptance — 네트워크 없이 순수 파싱만).</summary>
public sealed class VideoMetadataParsingTests
{
    [Fact]
    public void 정상_oEmbed_JSON에서_제목과_채널명을_추출한다()
    {
        // 실제 oEmbed 응답 형태 (title·author_name 외 필드는 무시 대상)
        const string json = """
            {
              "title": "테스트 영상",
              "author_name": "테스트 채널",
              "author_url": "https://www.youtube.com/@test",
              "type": "video",
              "provider_name": "YouTube",
              "thumbnail_url": "https://i.ytimg.com/vi/abc123def45/hqdefault.jpg"
            }
            """;

        var parsed = VideoMetadataService.TryParse(json, out var metadata);

        Assert.True(parsed);
        Assert.Equal("테스트 영상", metadata.Title);
        Assert.Equal("테스트 채널", metadata.ChannelName);
    }

    [Fact]
    public void 채널명이_없어도_제목만으로_성공한다()
    {
        var parsed = VideoMetadataService.TryParse("""{ "title": "제목만" }""", out var metadata);

        Assert.True(parsed);
        Assert.Equal("제목만", metadata.Title);
        Assert.Equal(string.Empty, metadata.ChannelName);
    }

    [Fact]
    public void 제목이_없으면_실패한다()
    {
        var parsed = VideoMetadataService.TryParse("""{ "author_name": "채널만" }""", out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData("{ 깨진 JSON !!!")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1, 2, 3]")]
    [InlineData("""{ "title": 123 }""")]
    public void 비정상_JSON은_예외_없이_실패한다(string json)
    {
        var parsed = VideoMetadataService.TryParse(json, out _);

        Assert.False(parsed);
    }
}
