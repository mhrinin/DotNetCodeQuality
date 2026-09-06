using System.IO.Compression;
using System.Text.RegularExpressions;
using DotNetCodeQuality.Tests.Harness;

namespace DotNetCodeQuality.Tests;

[Collection(nameof(PackageCollection))]
public sealed class PackageTests(PackageFixture fixture)
{
    [Fact]
    public void Nuspec_DeclaresAnalyzerDependenciesAsDevelopmentDependency()
    {
        using var archive = ZipFile.OpenRead(fixture.PackagePath);
        var nuspecEntry = archive.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var reader = new StreamReader(nuspecEntry.Open());
        var nuspec = reader.ReadToEnd();

        var dependencies = Regex.Matches(nuspec, "<dependency id=\"([^\"]+)\"").Select(m => m.Groups[1].Value).Order().ToList();

        Assert.Equal(["Microsoft.CodeAnalysis.BannedApiAnalyzers", "SonarAnalyzer.CSharp"], dependencies);
        Assert.Contains("<developmentDependency>true</developmentDependency>", nuspec);
    }

    [Fact]
    public void Package_HasNoLibFolder_AndShipsAllConfigurationFiles()
    {
        using var archive = ZipFile.OpenRead(fixture.PackagePath);
        var entries = archive.Entries.Select(e => e.FullName).ToList();

        Assert.DoesNotContain(entries, e => e.StartsWith("lib/", StringComparison.Ordinal));
        Assert.Contains("build/DotNetCodeQuality.props", entries);
        Assert.Contains("build/DotNetCodeQuality.targets", entries);
        Assert.Contains("buildTransitive/DotNetCodeQuality.props", entries);
        Assert.Contains("buildMultiTargeting/DotNetCodeQuality.targets", entries);
        foreach (var file in new[] { "Analysis", "Style", "Sonar", "Profile.App", "Profile.Library", "Profile.Test" })
        {
            Assert.Contains($"configuration/{file}.globalconfig", entries);
        }

        Assert.Contains("configuration/SonarLint.xml", entries);
        Assert.Contains("configuration/BannedSymbols.txt", entries);
    }

    [Fact]
    public async Task SonarConfigGenerator_ReportsCommittedConfigUpToDate()
    {
        var result = await DotnetCli.RunAsync(
            "run --project tools/SonarConfigGenerator -- --check",
            fixture.RepositoryRoot,
            TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("up to date", result.Output);
    }
}
