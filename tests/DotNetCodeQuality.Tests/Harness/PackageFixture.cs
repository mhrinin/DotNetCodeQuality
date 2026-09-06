namespace DotNetCodeQuality.Tests.Harness;

public sealed class PackageFixture : IAsyncLifetime
{
    public const string PackageVersion = "999.9.9";

    public string RepositoryRoot { get; } = FindRepositoryRoot();

    public string WorkRoot { get; } = Path.Combine(Path.GetTempPath(), "DotNetCodeQuality.Tests", Guid.NewGuid().ToString("N"));

    public string FeedDirectory => Path.Combine(WorkRoot, "feed");

    public string PackagesDirectory => Path.Combine(WorkRoot, "packages");

    public string PackagePath => Path.Combine(FeedDirectory, $"DotNetCodeQuality.{PackageVersion}.nupkg");

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(FeedDirectory);
        Directory.CreateDirectory(PackagesDirectory);

        var prebuilt = Environment.GetEnvironmentVariable("DOTNETCODEQUALITY_NUPKG");
        if (prebuilt is { Length: > 0 } && File.Exists(prebuilt))
        {
            File.Copy(prebuilt, PackagePath, overwrite: true);
            return;
        }

        var projectPath = Path.Combine(RepositoryRoot, "src", "DotNetCodeQuality");
        var result = await DotnetCli.RunAsync(
            $"pack \"{projectPath}\" -c Release -o \"{FeedDirectory}\" -p:MinVerVersionOverride={PackageVersion} -nologo",
            RepositoryRoot,
            TestContext.Current.CancellationToken);

        if (result.ExitCode != 0 || !File.Exists(PackagePath))
        {
            throw new InvalidOperationException("dotnet pack failed:" + Environment.NewLine + result.Output);
        }
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            Directory.Delete(WorkRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return ValueTask.CompletedTask;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "DotNetCodeQuality", "DotNetCodeQuality.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found above " + AppContext.BaseDirectory);
    }
}

[CollectionDefinition(nameof(PackageCollection))]
public sealed class PackageCollection : ICollectionFixture<PackageFixture>;
