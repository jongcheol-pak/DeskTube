using Xunit;

namespace DeskTube.Tests;

/// <summary>
/// 테스트 인프라 동작 확인용 스모크 테스트 (T1 acceptance: 빈 테스트 1개 통과).
/// </summary>
public class SmokeTests
{
    [Fact]
    public void 테스트_인프라가_동작한다()
    {
        Assert.True(true);
    }
}
