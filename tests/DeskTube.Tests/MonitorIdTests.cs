using DeskTube.Services;
using Xunit;

namespace DeskTube.Tests;

/// <summary>모니터 ID 생성·선택/오디오 대상 해석 로직 검증 (plan T4 acceptance — 가짜 모니터 데이터).</summary>
public sealed class MonitorIdTests
{
    private static MonitorInfo Make(string device, int x, int y, bool primary = false) =>
        new(MonitorService.CreateMonitorId(device, x, y), device, x, y, 1920, 1080, primary);

    [Fact]
    public void 모니터_ID는_디바이스명과_위치로_결정된다()
    {
        Assert.Equal(
            MonitorService.CreateMonitorId(@"\\.\DISPLAY1", 0, 0),
            MonitorService.CreateMonitorId(@"\\.\DISPLAY1", 0, 0));

        Assert.NotEqual(
            MonitorService.CreateMonitorId(@"\\.\DISPLAY1", 0, 0),
            MonitorService.CreateMonitorId(@"\\.\DISPLAY2", 0, 0));

        Assert.NotEqual(
            MonitorService.CreateMonitorId(@"\\.\DISPLAY1", 0, 0),
            MonitorService.CreateMonitorId(@"\\.\DISPLAY1", 1920, 0));
    }

    [Fact]
    public void 저장된_선택과_일치하는_모니터들이_해석된다()
    {
        var a = Make(@"\\.\DISPLAY1", 0, 0, primary: true);
        var b = Make(@"\\.\DISPLAY2", 1920, 0);
        var c = Make(@"\\.\DISPLAY3", 3840, 0);

        var targets = MonitorService.ResolveTargets([a, b, c], [b.Id, c.Id]);

        Assert.Equal(2, targets.Count);
        Assert.DoesNotContain(a, targets);
    }

    [Fact]
    public void 선택이_없거나_무효하면_주_모니터로_폴백한다()
    {
        var a = Make(@"\\.\DISPLAY1", 0, 0, primary: true);
        var b = Make(@"\\.\DISPLAY2", 1920, 0);

        Assert.Equal([a], MonitorService.ResolveTargets([a, b], []));
        Assert.Equal([a], MonitorService.ResolveTargets([a, b], ["없는ID"]));
    }

    [Fact]
    public void 주_모니터_플래그가_없으면_첫_모니터로_폴백한다()
    {
        var a = Make(@"\\.\DISPLAY1", 0, 0);
        var b = Make(@"\\.\DISPLAY2", 1920, 0);

        Assert.Equal([a], MonitorService.ResolveTargets([a, b], []));
    }

    [Fact]
    public void 모니터가_없으면_빈_목록을_반환한다()
    {
        Assert.Empty(MonitorService.ResolveTargets([], ["아무거나"]));
    }

    [Fact]
    public void 오디오_대상은_지정_ID_주모니터_첫대상_순으로_폴백한다()
    {
        var a = Make(@"\\.\DISPLAY1", 0, 0, primary: true);
        var b = Make(@"\\.\DISPLAY2", 1920, 0);

        Assert.Equal(b, MonitorService.ResolveAudioTarget([a, b], b.Id)); // 지정 ID
        Assert.Equal(a, MonitorService.ResolveAudioTarget([a, b], "없는ID")); // 주 모니터 폴백
        Assert.Equal(a, MonitorService.ResolveAudioTarget([a, b], null)); // 미지정 → 주 모니터
        Assert.Equal(b, MonitorService.ResolveAudioTarget([b], null)); // 주 모니터 부재 → 첫 대상
        Assert.Null(MonitorService.ResolveAudioTarget([], null)); // 대상 없음
    }

    [Fact]
    public void 실제_모니터_열거는_최소_1대와_주_모니터_1대를_반환한다()
    {
        // interop 스모크 — EnumDisplayMonitors는 일반 프로세스에서 동작 (테스트 머신엔 항상 모니터 1대 이상)
        using var service = new MonitorService();
        var monitors = service.GetMonitors();

        Assert.NotEmpty(monitors);
        Assert.Single(monitors, m => m.IsPrimary);
        Assert.All(monitors, m =>
        {
            Assert.False(string.IsNullOrEmpty(m.Id));
            Assert.True(m.Width > 0 && m.Height > 0);
        });
    }
}
