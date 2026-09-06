using DotNetCodeQuality.Tests.Harness;

namespace DotNetCodeQuality.Tests;

[Collection(nameof(PackageCollection))]
public sealed class ProfileTests(PackageFixture fixture)
{
    private const string UnderscoreMethod = """
        namespace Sample;

        public static class Naming
        {
            public static int Foo_Bar() => 1;
        }
        """;

    private const string AwaitWithoutConfigureAwait = """
        namespace Sample;

        public static class Work
        {
            public static async System.Threading.Tasks.Task RunAsync() => await System.Threading.Tasks.Task.Delay(1);
        }
        """;

    [Fact]
    public async Task XunitV3Project_IsDetectedAsTest()
    {
        var result = await new ProjectBuilder(fixture)
            .WithPackage("xunit.v3", "3.2.2")
            .WithSource("Naming.cs", UnderscoreMethod)
            .WithSource("SampleTests.cs", "namespace Sample;\n\npublic sealed class SampleTests\n{\n    [Xunit.Fact]\n    public void Foo_Bar() => Xunit.Assert.Equal(1, Naming.Foo_Bar());\n}\n")
            .BuildAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.False(result.Has("CA1707"), result.Output);
    }

    [Fact]
    public async Task TestSdkWithXunitV2_IsDetectedAsTest()
    {
        var result = await new ProjectBuilder(fixture)
            .WithPackage("Microsoft.NET.Test.Sdk", "17.14.1")
            .WithPackage("xunit", "2.9.3")
            .WithSource("Naming.cs", UnderscoreMethod)
            .BuildAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.False(result.Has("CA1707"), result.Output);
    }

    [Fact]
    public async Task ExplicitAppProfile_OverridesTestDetection()
    {
        var result = await new ProjectBuilder(fixture)
            .WithPackage("xunit.v3", "3.2.2")
            .WithProperty("DotNetCodeQualityProfile", "App")
            .WithSource("Naming.cs", UnderscoreMethod)
            .BuildAsync();

        Assert.True(result.HasError("CA1707"), result.Output);
    }

    [Fact]
    public async Task AppProfile_ReportsUnderscoreNames()
    {
        var result = await new ProjectBuilder(fixture)
            .WithSource("Program.cs", "namespace Sample;\n\npublic static class Program\n{\n    public static void Main() => System.Console.WriteLine(Naming.Foo_Bar());\n}\n")
            .WithSource("Naming.cs", UnderscoreMethod)
            .BuildAsync();

        Assert.True(result.HasError("CA1707"), result.Output);
    }

    [Fact]
    public async Task Library_RequiresConfigureAwait()
    {
        var result = await new ProjectBuilder(fixture).AsLibrary().WithSource("Work.cs", AwaitWithoutConfigureAwait).BuildAsync();

        Assert.True(result.HasError("CA2007"), result.Output);
    }

    [Fact]
    public async Task ConsoleApp_DoesNotRequireConfigureAwait()
    {
        var result = await new ProjectBuilder(fixture)
            .WithSource("Work.cs", AwaitWithoutConfigureAwait)
            .WithSource("Program.cs", "namespace Sample;\n\npublic static class Program\n{\n    public static System.Threading.Tasks.Task Main() => Work.RunAsync();\n}\n")
            .BuildAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.False(result.Has("CA2007"), result.Output);
    }

    [Fact]
    public async Task WebProject_DoesNotRequireConfigureAwait()
    {
        var result = await new ProjectBuilder(fixture)
            .WithSdk("Microsoft.NET.Sdk.Web")
            .WithSource("Work.cs", AwaitWithoutConfigureAwait)
            .WithSource("Program.cs", "namespace Sample;\n\npublic static class Program\n{\n    public static System.Threading.Tasks.Task Main() => Work.RunAsync();\n}\n")
            .BuildAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.False(result.Has("CA2007"), result.Output);
    }

    [Fact]
    public async Task InvalidProfile_FailsTheBuildWithAClearMessage()
    {
        var result = await new ProjectBuilder(fixture)
            .WithProperty("DotNetCodeQualityProfile", "Desktop")
            .WithSource("Naming.cs", UnderscoreMethod)
            .BuildAsync();

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("DotNetCodeQualityProfile must be App, Library or Test", result.Output);
    }
}
