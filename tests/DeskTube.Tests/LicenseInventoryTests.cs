using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace DeskTube.Tests;

/// <summary>
/// 라이선스 목록 무결성 검증 (PRD FR-12, plan T6·D5) —
/// 앱 csproj의 PackageReference 집합 ⊆ Assets/licenses/index.json 집합을 빌드 게이트로 강제한다.
/// </summary>
public sealed class LicenseInventoryTests
{
    /// <summary>빌드 시점 전용 패키지 — 앱 배포 산출물에 포함되지 않아 라이선스 화면 대상이 아니다.</summary>
    private static readonly HashSet<string> BuildOnlyPackages = ["Microsoft.Windows.SDK.BuildTools"];

    [Fact]
    public void 앱이_참조하는_모든_패키지가_라이선스_목록에_있다()
    {
        var root = FindRepoRoot();
        var csproj = XDocument.Load(Path.Combine(root, "src", "DeskTube", "DeskTube.csproj"));
        var references = csproj.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value)
            .Where(id => id is not null && !BuildOnlyPackages.Contains(id))
            .Select(id => id!)
            .ToHashSet();

        Assert.NotEmpty(references);

        var listed = ReadIndexIds(root);
        var missing = references.Except(listed).ToList();
        Assert.True(missing.Count == 0, $"라이선스 목록(index.json)에 누락된 패키지: {string.Join(", ", missing)}");
    }

    [Fact]
    public void 모든_패키지에_공식_사이트_url이_있다()
    {
        var root = FindRepoRoot();
        var indexPath = Path.Combine(root, "src", "DeskTube", "Assets", "licenses", "index.json");
        using var index = JsonDocument.Parse(File.ReadAllText(indexPath));

        foreach (var package in index.RootElement.GetProperty("packages").EnumerateArray())
        {
            var id = package.GetProperty("id").GetString()!;
            Assert.True(package.TryGetProperty("url", out var url), $"url 필드 없음: {id}");
            var value = url.GetString();
            Assert.False(string.IsNullOrWhiteSpace(value), $"url이 비어 있음: {id}");
            Assert.StartsWith("https://", value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void 라이선스_전문_파일이_모두_존재하고_비어있지_않다()
    {
        var root = FindRepoRoot();
        var dir = Path.Combine(root, "src", "DeskTube", "Assets", "licenses");
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "index.json")));

        foreach (var package in index.RootElement.GetProperty("packages").EnumerateArray())
        {
            var file = package.GetProperty("file").GetString()!;
            var path = Path.Combine(dir, file);
            Assert.True(File.Exists(path), $"라이선스 전문 파일 없음: {file}");
            Assert.True(new FileInfo(path).Length > 0, $"라이선스 전문이 비어 있음: {file}");
        }
    }

    private static HashSet<string> ReadIndexIds(string root)
    {
        var indexPath = Path.Combine(root, "src", "DeskTube", "Assets", "licenses", "index.json");
        using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
        return index.RootElement.GetProperty("packages").EnumerateArray()
            .Select(p => p.GetProperty("id").GetString()!)
            .ToHashSet();
    }

    /// <summary>테스트 출력 폴더에서 저장소 루트(DeskTube.slnx 위치)까지 상향 탐색.</summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DeskTube.slnx")))
        {
            dir = dir.Parent!;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
