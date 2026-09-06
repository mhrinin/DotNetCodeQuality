using DotNetCodeQuality.Tests.Harness;

namespace DotNetCodeQuality.Tests;

[Collection(nameof(PackageCollection))]
public sealed class SdkMatrixTests(PackageFixture fixture)
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
                    if (now.Year > 2000) System.Console.WriteLine(now);
                }
            }
        }
        """;

    [Theory]
    [InlineData("6.0.100", "net6.0")]
    [InlineData("8.0.100", "net8.0")]
    [InlineData("9.0.100", "net9.0")]
    [InlineData("10.0.100", "net10.0")]
    public async Task StrictBuild_FailsOnEverySupportedSdk(string sdkVersion, string targetFramework)
    {
        if (!await SdkIsInstalledAsync(sdkVersion))
        {
            Assert.Skip($"SDK {sdkVersion} is not installed");
        }

        var result = await new ProjectBuilder(fixture)
            .WithSdkVersion(sdkVersion)
            .WithTargetFramework(targetFramework)
            .WithSource("Program.cs", ViolatingProgram)
            .BuildAsync();

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(result.HasError("S1481"), result.Output);
        Assert.True(result.HasError("IDE0011"), result.Output);
        Assert.True(result.HasError("RS0030"), result.Output);
        Assert.False(result.Has("CS8032"), result.Output);
    }

    private static async Task<bool> SdkIsInstalledAsync(string sdkVersion)
    {
        var required = Version.Parse(sdkVersion);
        var listed = await DotnetCli.RunAsync("--list-sdks", Path.GetTempPath(), TestContext.Current.CancellationToken);
        return listed.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Split(' ')[0])
            .Where(token => !token.Contains('-', StringComparison.Ordinal) && Version.TryParse(token, out _))
            .Select(Version.Parse)
            .Any(installed => installed.Major == required.Major && installed >= required);
    }
}
