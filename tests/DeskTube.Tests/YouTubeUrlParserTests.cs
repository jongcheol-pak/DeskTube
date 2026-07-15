using DeskTube.Models;
using DeskTube.Services;
using Xunit;

namespace DeskTube.Tests;

/// <summary>YouTubeUrlParser 정상/비정상 케이스 검증 (plan T3 acceptance — PRD FR-1).</summary>
public sealed class YouTubeUrlParserTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    public void 대표_형식을_파싱한다(string url, string expectedId)
    {
        var result = YouTubeUrlParser.Parse(url);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedId, result.Value);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=30s")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLabc")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=share_token")]
    public void 추가_쿼리는_무시하고_영상_ID만_추출한다(string url)
    {
        var result = YouTubeUrlParser.Parse(url);

        Assert.True(result.IsSuccess);
        Assert.Equal("dQw4w9WgXcQ", result.Value);
    }

    [Fact]
    public void 스킴_없는_입력도_허용한다()
    {
        var result = YouTubeUrlParser.Parse("youtube.com/watch?v=dQw4w9WgXcQ");

        Assert.True(result.IsSuccess);
        Assert.Equal("dQw4w9WgXcQ", result.Value);
    }

    [Fact]
    public void 앞뒤_공백은_제거_후_파싱한다()
    {
        var result = YouTubeUrlParser.Parse("  https://youtu.be/dQw4w9WgXcQ  ");

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("https://vimeo.com/12345678")]
    [InlineData("https://www.youtube.com/watch?v=too_short")]
    [InlineData("https://www.youtube.com/playlist?list=PLabc")]
    [InlineData("그냥 텍스트")]
    public void 비정상_입력은_InvalidInput으로_거부한다(string? input)
    {
        var result = YouTubeUrlParser.Parse(input);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidInput, result.Code);
    }
}
