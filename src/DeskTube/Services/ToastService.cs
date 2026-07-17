using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace DeskTube.Services;

/// <summary>
/// 앱 공용 토스트 알림 라우팅 (toast plan T1·D1) — VM·App은 Show만 호출하고
/// 표시는 MainWindow의 호스트(presenter)가 담당한다. 창 재생성(언어 전환) 시
/// 새 창의 Attach가 presenter를 덮어써 옛 창 참조가 해제된다.
/// </summary>
public static class ToastService
{
    private static Action<string, InfoBarSeverity>? _presenter;
    private static DispatcherQueue? _dispatcher;

    /// <summary>토스트 호스트 등록 — MainWindow 생성자에서 호출 (UI 스레드 전제).</summary>
    public static void Attach(Action<string, InfoBarSeverity> presenter, DispatcherQueue dispatcher)
    {
        _presenter = presenter;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// 토스트 표시 — 자동 소멸(성공/정보 3초·오류/경고 5초, D2)은 호스트가 처리.
    /// 발생 스레드 무관(디스패처 마셜링), 호스트 미등록이면 무시(방어 — 실경로 없음).
    /// </summary>
    public static void Show(string message, InfoBarSeverity severity = InfoBarSeverity.Informational) =>
        _dispatcher?.TryEnqueue(() => _presenter?.Invoke(message, severity));
}
