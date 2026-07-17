using DeskTube.Services;
using DeskTube.ViewModels;

namespace DeskTube.Views;

/// <summary>
/// 로그인 창 열기 공통 흐름 (FR-15) — 홈·설정 코드비하인드가 공유한다
/// (창 생성은 View 계층 담당이라는 MVVM 경계는 유지하되, 동일 로직의 중복 구현을 제거 — 2026-07-17).
/// </summary>
internal static class LoginFlow
{
    /// <summary>로그인 창을 띄우고, 닫히면 세션 상태를 재확인한다 (도중 닫기 = 상태 변화 없음).</summary>
    public static void Open(AccountPanelViewModel account)
    {
        var login = new LoginWindow();
        login.Closed += async (_, _) =>
        {
            try
            {
                await account.RefreshSessionAsync();
            }
            catch (Exception ex)
            {
                AppLog.Write($"로그인 후 상태 갱신 실패: {ex.GetType().Name} {ex.Message}");
            }
        };
        login.Activate();
    }
}
