using DeskTube.Models;
using DeskTube.Services;
using Xunit;

namespace DeskTube.Tests;

/// <summary>PlaybackQueue 모드별 다음 곡 결정 검증 (plan T3 acceptance — PRD FR-7).</summary>
public sealed class PlaybackQueueTests
{
    private static List<PlaylistItem> MakeItems(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new PlaylistItem { Url = $"url{i}", VideoId = $"video{i:D6}" })];

    [Fact]
    public void 순차_모드는_순서대로_진행하고_끝에서_정지한다()
    {
        var items = MakeItems(3);
        var queue = new PlaybackQueue(items, PlaybackMode.Sequential);

        Assert.Equal(items[0].Id, queue.Start()!.Id);
        Assert.Equal(items[1].Id, queue.Next()!.Id);
        Assert.Equal(items[2].Id, queue.Next()!.Id);
        Assert.Null(queue.Next()); // 끝 — 정지
    }

    [Fact]
    public void 전체반복_모드는_끝에서_처음으로_돌아온다()
    {
        var items = MakeItems(3);
        var queue = new PlaybackQueue(items, PlaybackMode.RepeatAll);

        queue.Start();
        queue.Next();
        queue.Next(); // 마지막 곡
        Assert.Equal(items[0].Id, queue.Next()!.Id); // 처음으로
    }

    [Fact]
    public void 한곡반복_모드는_같은_곡을_유지한다()
    {
        var items = MakeItems(3);
        var queue = new PlaybackQueue(items, PlaybackMode.RepeatOne);

        var first = queue.Start()!;
        Assert.Equal(first.Id, queue.Next()!.Id);
        Assert.Equal(first.Id, queue.Next()!.Id);
    }

    [Fact]
    public void 셔플_모드는_한_사이클에_전곡을_1회씩_소진한다()
    {
        var items = MakeItems(10);
        var queue = new PlaybackQueue(items, PlaybackMode.Shuffle, seed: 42);

        var played = new List<Guid> { queue.Start()!.Id };
        for (var i = 1; i < items.Count; i++)
        {
            played.Add(queue.Next()!.Id);
        }

        // 사이클 내 중복 없음 + 전곡 포함 (순서만 무작위)
        Assert.Equal(items.Count, played.Distinct().Count());
        Assert.Equal(items.Select(i => i.Id).OrderBy(g => g), played.OrderBy(g => g));
    }

    [Fact]
    public void 셔플_사이클_소진_후_재셔플로_계속되고_직전_곡이_연속되지_않는다()
    {
        var items = MakeItems(5);
        var queue = new PlaybackQueue(items, PlaybackMode.Shuffle, seed: 7);

        queue.Start();
        Guid last = default;
        for (var i = 1; i < items.Count; i++)
        {
            last = queue.Next()!.Id;
        }

        var nextCycleFirst = queue.Next(); // 소진 → 재셔플
        Assert.NotNull(nextCycleFirst);
        Assert.NotEqual(last, nextCycleFirst!.Id); // 연속 반복 회피
    }

    [Fact]
    public void 항목_1개_셔플은_같은_곡_반복을_허용한다()
    {
        var items = MakeItems(1);
        var queue = new PlaybackQueue(items, PlaybackMode.Shuffle, seed: 1);

        Assert.Equal(items[0].Id, queue.Start()!.Id);
        Assert.Equal(items[0].Id, queue.Next()!.Id);
        Assert.Equal(items[0].Id, queue.Next()!.Id);
    }

    [Fact]
    public void 랜덤_모드는_항상_다음_곡을_반환한다()
    {
        var items = MakeItems(3);
        var queue = new PlaybackQueue(items, PlaybackMode.Random, seed: 99);

        queue.Start();
        for (var i = 0; i < 20; i++)
        {
            Assert.NotNull(queue.Next()); // 중복 허용, 끝 없음
        }
    }

    [Fact]
    public void 빈_목록은_시작과_다음_모두_null이다()
    {
        var queue = new PlaybackQueue([], PlaybackMode.Sequential);

        Assert.Null(queue.Start());
        Assert.Null(queue.Next());
    }

    [Fact]
    public void 모드_변경_시_현재_곡은_유지된다()
    {
        var items = MakeItems(5);
        var queue = new PlaybackQueue(items, PlaybackMode.Sequential);
        queue.Start();
        var current = queue.Current!;

        queue.SetMode(PlaybackMode.Shuffle);

        Assert.Equal(current.Id, queue.Current!.Id);
    }

    [Fact]
    public void 목록_갱신_시_현재_곡이_남아있으면_유지된다()
    {
        var items = MakeItems(4);
        var queue = new PlaybackQueue(items, PlaybackMode.Sequential);
        queue.Start();
        queue.Next(); // items[1]

        var updated = new List<PlaylistItem> { items[3], items[1], items[0] };
        queue.UpdateItems(updated);

        Assert.Equal(items[1].Id, queue.Current!.Id);
        Assert.Equal(items[0].Id, queue.Next()!.Id); // 새 목록 기준 다음
    }

    [Fact]
    public void 셔플_모드에서_현재_곡이_삭제돼도_같은_곡_연속과_곡_누락이_없다()
    {
        var items = MakeItems(5);
        var queue = new PlaybackQueue(items, PlaybackMode.Shuffle, seed: 13);
        queue.Start();
        var deleted = queue.Current!;

        var remaining = items.Where(i => i.Id != deleted.Id).ToList();
        queue.UpdateItems(remaining);
        var corrected = queue.Current!; // 보정된 현재 곡 (사이클 0번 앵커)

        // 이후 Next() 3회 = 사이클 나머지 전곡 — 보정 곡과 중복 없이 잔여 곡 전부를 커버해야 한다
        var played = new List<Guid>();
        for (var i = 0; i < remaining.Count - 1; i++)
        {
            played.Add(queue.Next()!.Id);
        }

        Assert.DoesNotContain(corrected.Id, played); // 같은 곡 연속/재등장 없음 (사이클 내)
        Assert.Equal(remaining.Count - 1, played.Distinct().Count()); // 곡 누락 없음
        Assert.Equal(
            remaining.Select(i => i.Id).Where(id => id != corrected.Id).OrderBy(g => g),
            played.OrderBy(g => g));
    }

    [Fact]
    public void 목록_갱신_시_현재_곡이_삭제됐으면_다음_곡으로_이어간다()
    {
        var items = MakeItems(3);
        var queue = new PlaybackQueue(items, PlaybackMode.Sequential);
        queue.Start(); // items[0]

        queue.UpdateItems([items[1], items[2]]); // 현재 곡 삭제

        Assert.NotNull(queue.Next()); // 진행 계속 가능
    }
}
