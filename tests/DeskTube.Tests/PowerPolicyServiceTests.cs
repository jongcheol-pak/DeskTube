using DeskTube.Models;
using DeskTube.Services;
using Xunit;

namespace DeskTube.Tests;

/// <summary>PowerPolicyService 신호 합성 상태 머신 검증 (plan T8 acceptance — 신호 주입).</summary>
public sealed class PowerPolicyServiceTests
{
    private sealed class Recorder
    {
        public int PauseCount;
        public int ResumeCount;

        public Recorder(PowerPolicyService service)
        {
            service.PauseRequested += (_, _) => PauseCount++;
            service.ResumeRequested += (_, _) => ResumeCount++;
        }
    }

    private static (PowerPolicyService Service, Recorder Recorder, AppSettings Settings) Create()
    {
        var settings = new AppSettings(); // 기본: 3정책 모두 켬
        var service = new PowerPolicyService(settings);
        return (service, new Recorder(service), settings);
    }

    [Fact]
    public void 전체화면_켜짐_꺼짐에_일시정지와_재개가_한_번씩_발행된다()
    {
        var (service, recorder, _) = Create();

        service.SetSignal(PowerSignal.Fullscreen, true);
        Assert.Equal(1, recorder.PauseCount);
        Assert.Equal(0, recorder.ResumeCount);

        service.SetSignal(PowerSignal.Fullscreen, false);
        Assert.Equal(1, recorder.PauseCount);
        Assert.Equal(1, recorder.ResumeCount);
    }

    [Fact]
    public void 신호가_겹치면_모두_해제될_때만_재개된다()
    {
        var (service, recorder, _) = Create();

        service.SetSignal(PowerSignal.BatterySaver, true); // pause
        service.SetSignal(PowerSignal.SessionLock, true);  // 추가 신호 — 발행 없음

        Assert.Equal(1, recorder.PauseCount);

        service.SetSignal(PowerSignal.SessionLock, false); // 세이버 여전히 on — 재개 안 함 (plan Edge Case)
        Assert.Equal(0, recorder.ResumeCount);

        service.SetSignal(PowerSignal.BatterySaver, false); // 모두 해제 — 재개
        Assert.Equal(1, recorder.ResumeCount);
    }

    [Fact]
    public void 같은_신호_중복_입력은_한_번만_발행한다()
    {
        var (service, recorder, _) = Create();

        service.SetSignal(PowerSignal.Fullscreen, true);
        service.SetSignal(PowerSignal.Fullscreen, true); // 폴링 반복 입력

        Assert.Equal(1, recorder.PauseCount);
    }

    [Fact]
    public void 설정에서_끈_정책의_신호는_무시된다()
    {
        var (service, recorder, settings) = Create();
        settings.PauseOnFullscreen = false;

        service.SetSignal(PowerSignal.Fullscreen, true);

        Assert.Equal(0, recorder.PauseCount);
    }

    [Fact]
    public void 끈_정책과_켠_정책이_섞이면_켠_정책만_반영된다()
    {
        var (service, recorder, settings) = Create();
        settings.PauseOnFullscreen = false;

        service.SetSignal(PowerSignal.Fullscreen, true); // 무시
        service.SetSignal(PowerSignal.SessionLock, true); // pause

        Assert.Equal(1, recorder.PauseCount);

        service.SetSignal(PowerSignal.SessionLock, false); // 전체화면 신호가 남아있어도 꺼진 정책 — 재개
        Assert.Equal(1, recorder.ResumeCount);
    }

    [Fact]
    public void 일시정지_중_해당_정책을_끄고_재평가하면_재개된다()
    {
        var (service, recorder, settings) = Create();

        service.SetSignal(PowerSignal.BatterySaver, true);
        Assert.Equal(1, recorder.PauseCount);

        settings.PauseOnBatterySaver = false;
        service.Reevaluate(); // part2 설정 UI 경로

        Assert.Equal(1, recorder.ResumeCount);
    }

    [Fact]
    public void 신호가_없으면_재평가해도_아무것도_발행하지_않는다()
    {
        var (service, recorder, _) = Create();

        service.Reevaluate();

        Assert.Equal(0, recorder.PauseCount);
        Assert.Equal(0, recorder.ResumeCount);
    }
}
