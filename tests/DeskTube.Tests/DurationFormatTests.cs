using DeskTube.ViewModels;
using Xunit;

namespace DeskTube.Tests;

/// <summary>PlaylistItemEntry.FormatDuration 경계값 검증 (FR-18, item-duration plan T4).</summary>
public sealed class DurationFormatTests
{
    [Theory]
    [InlineData(0, "")]        // 미수집 — 공란
    [InlineData(-5, "")]       // 비정상 음수도 공란
    [InlineData(59, "0:59")]   // 1분 미만
    [InlineData(60, "1:00")]   // 정확히 1분
    [InlineData(204, "3:24")]  // 일반 곡
    [InlineData(3599, "59:59")] // 1시간 직전
    [InlineData(3600, "1:00:00")] // 정확히 1시간 — h:mm:ss 전환
    [InlineData(7325, "2:02:05")] // 2시간 2분 5초
    public void 재생시간_포맷_경계값(int seconds, string expected)
    {
        Assert.Equal(expected, PlaylistItemEntry.FormatDuration(seconds));
    }
}
