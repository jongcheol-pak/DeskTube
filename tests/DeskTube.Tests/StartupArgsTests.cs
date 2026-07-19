using DeskTube.Services;
using Microsoft.Windows.AppLifecycle;
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

    // ---- IsQuietActivation (FR-22 — 리다이렉트 수신 활성화 판별) ----

    [Fact]
    public void StartupTask_활성화는_조용한_활성화다()
    {
        Assert.True(StartupArgs.IsQuietActivation(ExtendedActivationKind.StartupTask, []));
    }

    [Fact]
    public void 일반_실행이라도_startup_플래그가_있으면_조용한_활성화다()
    {
        Assert.True(StartupArgs.IsQuietActivation(ExtendedActivationKind.Launch, ["DeskTube.exe", "-startup"]));
    }

    [Fact]
    public void 일반_실행은_조용한_활성화가_아니다()
    {
        Assert.False(StartupArgs.IsQuietActivation(ExtendedActivationKind.Launch, ["DeskTube.exe"]));
        Assert.False(StartupArgs.IsQuietActivation(ExtendedActivationKind.Launch, []));
    }

    [Fact]
    public void null_인자_요소는_조용한_활성화_판별에_무해하다()
    {
        Assert.False(StartupArgs.IsQuietActivation(ExtendedActivationKind.Launch, [null, string.Empty]));
    }
}
