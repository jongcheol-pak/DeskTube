using DeskTube.Services;
using Xunit;

namespace DeskTube.Tests;

/// <summary>시작 인자 → 자동 시작(트레이 조용 시작) 판별 검증 (plan T4 acceptance).</summary>
public sealed class StartupArgsTests
{
    [Fact]
    public void startup_플래그가_있으면_자동_시작이다()
    {
        Assert.True(StartupArgs.HasStartupFlag(["DeskTube.exe", "-startup"]));
    }

    [Fact]
    public void 대소문자와_앞뒤_공백을_무시한다()
    {
        Assert.True(StartupArgs.HasStartupFlag([" -Startup "]));
        Assert.True(StartupArgs.HasStartupFlag(["-STARTUP"]));
    }

    [Fact]
    public void 플래그가_없으면_일반_시작이다()
    {
        Assert.False(StartupArgs.HasStartupFlag(["DeskTube.exe"]));
        Assert.False(StartupArgs.HasStartupFlag(["DeskTube.exe", "--startup-like", "startup"]));
    }

    [Fact]
    public void 빈_인자와_null_항목을_허용한다()
    {
        Assert.False(StartupArgs.HasStartupFlag([]));
        Assert.False(StartupArgs.HasStartupFlag([null, string.Empty]));
    }
}
