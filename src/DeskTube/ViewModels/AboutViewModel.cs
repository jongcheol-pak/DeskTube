using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using DeskTube.Services;

namespace DeskTube.ViewModels;

/// <summary>라이선스 화면 1행 — 패키지 ID·라이선스 종류·전문.</summary>
public sealed record LicenseEntry(string Id, string License, string FullText);

/// <summary>
/// 정보 화면 (PRD FR-11·12, plan T6·D5).
/// 버전은 패키지 manifest에서 조회, 라이선스 목록은 Assets/licenses/index.json(수동 관리 —
/// 누락은 LicenseInventoryTests가 빌드 게이트로 차단)을 읽는다.
/// </summary>
public partial class AboutViewModel : ObservableObject
{
    public AboutViewModel()
    {
        AppVersion = string.Empty;
    }

    /// <summary>앱 카드 정보 한 줄 — "버전 x.x.x.x · 개발자: ..." (시안, restyle T8).</summary>
    [ObservableProperty]
    public partial string AppVersion { get; set; }

    public ObservableCollection<LicenseEntry> Licenses { get; } = [];

    /// <summary>페이지 진입 시 호출 — 버전·라이선스 목록 채움 (Services 불필요, 독립 동작).</summary>
    public void Load()
    {
        AppVersion = string.Format(Loc.Get("About_InfoLineFormat"), ResolveVersion());
        LoadLicenses();
    }

    /// <summary>버전 — manifest(Package.Current)와 일치 (plan T6 acceptance). 비패키지 실행은 어셈블리 폴백.</summary>
    private static string ResolveVersion()
    {
        try
        {
            var v = Windows.ApplicationModel.Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }
        catch (Exception ex)
        {
            AppLog.Write($"패키지 버전 조회 실패(어셈블리 버전 폴백): {ex.GetType().Name}");
            return typeof(AboutViewModel).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
        }
    }

    private void LoadLicenses()
    {
        Licenses.Clear();
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "licenses");
            using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "index.json")));
            foreach (var package in index.RootElement.GetProperty("packages").EnumerateArray())
            {
                var id = package.GetProperty("id").GetString()!;
                var license = package.GetProperty("license").GetString()!;
                var file = package.GetProperty("file").GetString()!;
                var fullText = File.ReadAllText(Path.Combine(dir, file));
                Licenses.Add(new LicenseEntry(id, license, fullText));
            }
        }
        catch (Exception ex)
        {
            // 목록 파손은 테스트가 차단하므로 도달이 어렵다 — 방어적으로 로그만
            AppLog.Write($"라이선스 목록 로드 실패: {ex.GetType().Name} {ex.Message}");
        }
    }
}
