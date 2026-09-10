using DotNetCodeQuality.Tests.Harness;

namespace DotNetCodeQuality.Tests;

[Collection(nameof(PackageCollection))]
public sealed class BuildBehaviourTests(PackageFixture fixture)
{
    private const string ViolatingProgram = """
        namespace Sample
        {
            public static class Program
            {
                public static void Main()
                {
                    var unused = 1;
                    var now = System.DateTime.Now;
                    var stamp = new System.DateTime(2020, 1, 1);
                    if (now.Year > 2000) System.Console.WriteLine(stamp);
                }
            }
        }
        """;

    private const string CleanProgram = """
        namespace Sample;

        public static class Program
        {
            public static void Main()
            {
                var now = System.DateTime.UtcNow;
                if (now.Year > 2000)
                {
                    System.Console.WriteLine(now);
                }
            }
        }
        """;

    [Fact]
    public async Task StrictByDefault_ViolationsAreErrors()
    {
        var result = await new ProjectBuilder(fixture).WithSource("Program.cs", ViolatingProgram).BuildAsync();

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.HasError("S1481"), result.Output);
        Assert.True(result.HasError("IDE0011"), result.Output);
        Assert.True(result.HasError("IDE0161"), result.Output);
        Assert.True(result.HasError("RS0030"), result.Output);
    }

    [Fact]
    public async Task StrictOff_ViolationsAreWarnings()
    {
        var result = await new ProjectBuilder(fixture)
            .WithProperty("DotNetCodeQualityStrict", "false")
            .WithSource("Program.cs", ViolatingProgram)
            .BuildAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.True(result.HasWarning("S1481"), result.Output);
        Assert.True(result.HasWarning("IDE0011"), result.Output);
        Assert.True(result.HasWarning("RS0030"), result.Output);
        Assert.DoesNotContain(result.Diagnostics, d => d.Level == "error");
    }

    [Fact]
    public async Task CleanCode_BuildsGreen()
    {
        var result = await new ProjectBuilder(fixture).WithSource("Program.cs", CleanProgram).BuildAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task SonarDefaults_AreOff()
    {
        var result = await new ProjectBuilder(fixture).WithSource("Program.cs", ViolatingProgram).BuildAsync();

        Assert.False(result.Has("S6562"), result.Output);
    }

    [Fact]
    public async Task ConsumerEditorConfig_OverridesPackageRules()
    {
        var result = await new ProjectBuilder(fixture)
            .WithEditorConfig("[*.cs]\ndotnet_diagnostic.IDE0011.severity = none\n")
            .WithSource("Program.cs", ViolatingProgram)
            .BuildAsync();

        Assert.False(result.Has("IDE0011"), result.Output);
        Assert.True(result.HasError("IDE0161"), result.Output);
    }

    [Fact]
    public async Task SonarOff_RemovesEverySonarDiagnostic()
    {
        var result = await new ProjectBuilder(fixture)
            .WithProperty("DotNetCodeQualitySonar", "false")
            .WithSource("Program.cs", ViolatingProgram)
            .BuildAsync();

        Assert.Empty(result.IdsStartingWith("S"));
        Assert.True(result.HasError("IDE0011"), result.Output);
    }

    [Fact]
    public async Task BannedSymbolsOff_AllowsDateTimeNow()
    {
        var result = await new ProjectBuilder(fixture)
            .WithProperty("DotNetCodeQualityBannedSymbols", "false")
            .WithSource("Program.cs", ViolatingProgram)
            .BuildAsync();

        Assert.False(result.Has("RS0030"), result.Output);
    }

    [Fact]
    public async Task ConsumerBannedSymbols_AreAdditive()
    {
        var result = await new ProjectBuilder(fixture)
            .WithAdditionalFile("BannedSymbols.txt", "M:System.Console.WriteLine(System.Object);Use a logger\n")
            .WithSource("Program.cs", ViolatingProgram)
            .BuildAsync();

        Assert.Contains("DateTime.Now", result.Output);
        Assert.Contains("Use a logger", result.Output);
    }

    [Fact]
    public async Task StyleOff_KeepsSonarAndBannedApis()
    {
        var result = await new ProjectBuilder(fixture)
            .WithProperty("DotNetCodeQualityStyle", "false")
            .WithSource("Program.cs", ViolatingProgram)
            .BuildAsync();

        Assert.False(result.Has("IDE0011"), result.Output);
        Assert.True(result.HasError("S1481"), result.Output);
        Assert.True(result.HasError("RS0030"), result.Output);
    }

    [Fact]
    public async Task UndocumentedPublicMember_DoesNotReportCS1591()
    {
        var result = await new ProjectBuilder(fixture).WithSource("Program.cs", CleanProgram).BuildAsync();

        Assert.False(result.Has("CS1591"), result.Output);
    }

    [Fact]
    public async Task VulnerablePackage_FailsRestoreWhenStrict()
    {
        var result = await new ProjectBuilder(fixture)
            .WithTargetFramework("net8.0")
            .WithPackage("System.Text.Json", "8.0.0")
            .WithSource("Program.cs", CleanProgram)
            .BuildAsync();

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.HasError("NU1903") || result.HasError("NU1902"), result.Output);
    }
}
