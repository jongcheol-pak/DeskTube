using Microsoft.Windows.ApplicationModel.Resources;

namespace DeskTube.Services;

/// <summary>
/// 코드비하인드 문구 조회 헬퍼 (AGENTS 다국어 규칙 2).
/// 키 부재 시 키 자체를 반환해 번역 누락을 화면에서 드러낸다.
/// </summary>
public static class Loc
{
    private static ResourceLoader? _loader;

    /// <summary>언어 전환 후 캐시 무효화 — 다음 조회부터 새 언어 컨텍스트의 로더 사용 (plan T7).</summary>
    public static void Reset() => _loader = null;

    public static string Get(string key)
    {
        try
        {
            _loader ??= new ResourceLoader();
            var value = _loader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch
        {
            return key; // 리소스 시스템 미가용(테스트 호스트 등) — 키 노출 폴백
        }
    }
}
