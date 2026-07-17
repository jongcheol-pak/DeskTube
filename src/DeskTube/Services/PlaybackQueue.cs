using DeskTube.Models;

namespace DeskTube.Services;

/// <summary>
/// 재생 순서 결정 (PRD FR-7) — 순차/셔플/랜덤/한곡반복/전체반복.
/// 순수 로직(외부 의존 없음). 모든 모드는 목록 끝에서 정지하지 않고 계속 반복한다
/// (순차: 끝나면 처음부터 — 2026-07-17 FR-7 갱신). 셔플은 사이클 내 전곡 1회 소진을
/// 보장하고, 소진 후에는 재셔플해 계속 이어간다 (1곡 리스트의 반복 재생 허용).
/// </summary>
public sealed class PlaybackQueue
{
    private readonly List<PlaylistItem> _items;
    private readonly Random _random;
    private PlaybackMode _mode;

    /// <summary>현재 항목의 _items 인덱스 (-1 = 시작 전/재생할 것 없음).</summary>
    private int _currentIndex = -1;

    /// <summary>셔플 사이클의 인덱스 순열과 진행 위치.</summary>
    private List<int> _shuffleOrder = [];
    private int _shufflePosition = -1;

    /// <param name="seed">테스트 재현용 난수 시드 (앱에서는 null).</param>
    public PlaybackQueue(IEnumerable<PlaylistItem> items, PlaybackMode mode, int? seed = null)
    {
        _items = [.. items];
        _mode = mode;
        _random = seed is null ? new Random() : new Random(seed.Value);
    }

    public PlaybackMode Mode => _mode;

    public int Count => _items.Count;

    public PlaylistItem? Current =>
        _currentIndex >= 0 && _currentIndex < _items.Count ? _items[_currentIndex] : null;

    /// <summary>
    /// 재생을 시작하고 첫 항목을 반환한다. 빈 목록이면 null.
    /// startItemId 지정 시 그 항목부터 시작한다 (FR-18 행 재생) —
    /// 목록에 없으면(직전 삭제 등) 무시하고 모드별 기본 시작.
    /// </summary>
    public PlaylistItem? Start(Guid? startItemId = null)
    {
        if (_items.Count == 0)
        {
            return null;
        }

        var startIndex = startItemId is null ? -1 : _items.FindIndex(i => i.Id == startItemId);

        if (_mode == PlaybackMode.Shuffle)
        {
            BuildShuffleCycle(avoidFirst: null);
            if (startIndex >= 0)
            {
                // 지정 곡을 사이클 첫 자리에 고정 — "전곡 1회 순회" 계약 유지 (SetMode와 동일 기법)
                _shuffleOrder.Remove(startIndex);
                _shuffleOrder.Insert(0, startIndex);
            }
            _shufflePosition = 0;
            _currentIndex = _shuffleOrder[0];
        }
        else if (_mode == PlaybackMode.Random)
        {
            _currentIndex = startIndex >= 0 ? startIndex : _random.Next(_items.Count);
        }
        else
        {
            _currentIndex = startIndex >= 0 ? startIndex : 0;
        }

        return Current;
    }

    /// <summary>
    /// 다음 항목을 결정한다. null = 빈 목록뿐 (모든 모드가 끝에서 순환 — FR-7).
    /// </summary>
    public PlaylistItem? Next()
    {
        if (_items.Count == 0)
        {
            _currentIndex = -1;
            return null;
        }

        switch (_mode)
        {
            case PlaybackMode.Sequential:
                // 목록 끝에서 정지하지 않고 처음부터 반복 (FR-7 — 시작 전 -1도 0으로 수렴)
                _currentIndex = (_currentIndex + 1) % _items.Count;
                break;

            case PlaybackMode.Shuffle:
                _shufflePosition++;
                if (_shufflePosition >= _shuffleOrder.Count)
                {
                    // 사이클 소진 — 재셔플 (직전 곡이 새 사이클 첫 곡으로 바로 반복되지 않게 회피)
                    BuildShuffleCycle(avoidFirst: _currentIndex);
                    _shufflePosition = 0;
                }
                _currentIndex = _shuffleOrder[_shufflePosition];
                break;

            case PlaybackMode.Random:
                _currentIndex = _random.Next(_items.Count);
                break;

            case PlaybackMode.RepeatOne:
                if (_currentIndex < 0)
                {
                    _currentIndex = 0;
                }
                break;

            case PlaybackMode.RepeatAll:
                _currentIndex = (_currentIndex + 1) % _items.Count;
                break;
        }

        return Current;
    }

    /// <summary>재생 모드 변경 — 현재 곡은 유지하고 이후 순서만 새 모드를 따른다.</summary>
    public void SetMode(PlaybackMode mode)
    {
        if (_mode == mode)
        {
            return;
        }

        _mode = mode;
        if (mode == PlaybackMode.Shuffle)
        {
            // 현재 곡을 사이클 시작으로 두고 나머지를 섞는다
            BuildShuffleCycle(avoidFirst: null);
            if (_currentIndex >= 0)
            {
                _shuffleOrder.Remove(_currentIndex);
                _shuffleOrder.Insert(0, _currentIndex);
            }
            _shufflePosition = 0;
        }
    }

    /// <summary>
    /// 재생 중 목록 변경 반영 (plan part2 T3 Edge Case 대비) — 현재 곡이 남아 있으면 유지,
    /// 삭제됐으면 다음 Next()가 새 목록 기준으로 진행되도록 위치를 보정한다.
    /// </summary>
    public void UpdateItems(IEnumerable<PlaylistItem> items)
    {
        var currentId = Current?.Id;
        var newItems = items.ToList();

        _items.Clear();
        _items.AddRange(newItems);

        var newIndex = currentId is null ? -1 : _items.FindIndex(i => i.Id == currentId);
        _currentIndex = newIndex >= 0 ? newIndex
            : Math.Min(Math.Max(_currentIndex - 1, -1), _items.Count - 1); // 삭제됨 — 직전 위치로 보정

        if (_mode == PlaybackMode.Shuffle)
        {
            // 인덱스 순열이 무효화되므로 사이클 재구성.
            // 현재 곡(생존 시) 또는 보정된 위치의 곡을 사이클 시작점(0번)에 고정해
            // Current와 셔플 진행 상태를 항상 일치시킨다 — 불일치 시 같은 곡 연속 재생·곡 누락 발생.
            BuildShuffleCycle(avoidFirst: null);
            var anchor = newIndex >= 0 ? newIndex : _currentIndex;
            if (anchor >= 0 && anchor < _items.Count)
            {
                _shuffleOrder.Remove(anchor);
                _shuffleOrder.Insert(0, anchor);
            }
            _shufflePosition = 0;
        }
    }

    /// <summary>Fisher-Yates 셔플로 인덱스 순열 생성. avoidFirst가 첫 자리에 오면 교환해 연속 반복을 피한다.</summary>
    private void BuildShuffleCycle(int? avoidFirst)
    {
        _shuffleOrder = [.. Enumerable.Range(0, _items.Count)];
        for (var i = _shuffleOrder.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (_shuffleOrder[i], _shuffleOrder[j]) = (_shuffleOrder[j], _shuffleOrder[i]);
        }

        if (avoidFirst is int avoid && _shuffleOrder.Count > 1 && _shuffleOrder[0] == avoid)
        {
            (_shuffleOrder[0], _shuffleOrder[^1]) = (_shuffleOrder[^1], _shuffleOrder[0]);
        }
    }
}
