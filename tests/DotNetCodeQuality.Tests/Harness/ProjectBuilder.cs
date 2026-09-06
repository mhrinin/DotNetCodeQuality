using System.Text;

namespace DotNetCodeQuality.Tests.Harness;

public sealed class ProjectBuilder(PackageFixture fixture)
{
    private readonly Dictionary<string, string> _properties = [];
    private readonly List<(string Id, string Version)> _packages = [];
    private readonly List<string> _additionalFiles = [];
    private readonly Dictionary<string, string> _files = [];
    private string _sdk = "Microsoft.NET.Sdk";
    private string _outputType = "Exe";
    private string _targetFramework = "net10.0";
    private string? _globalJsonSdk;

    public string Directory { get; } = Path.Combine(fixture.WorkRoot, "projects", Guid.NewGuid().ToString("N"));

    public ProjectBuilder WithSdk(string sdk)
    {
        _sdk = sdk;
        return this;
    }

    public ProjectBuilder AsLibrary()
    {
        _outputType = "Library";
        return this;
    }

    public ProjectBuilder WithTargetFramework(string targetFramework)
    {
        _targetFramework = targetFramework;
        return this;
    }

    public ProjectBuilder WithProperty(string name, string value)
    {
        _properties[name] = value;
        return this;
    }

    public ProjectBuilder WithPackage(string id, string version)
    {
        _packages.Add((id, version));
        return this;
    }

    public ProjectBuilder WithSource(string fileName, string content)
    {
        _files[fileName] = content;
        return this;
    }

    public ProjectBuilder WithEditorConfig(string content)
    {
        _files[".editorconfig"] = "root = true\n\n" + content;
        return this;
    }

    public ProjectBuilder WithAdditionalFile(string fileName, string content)
    {
        _files[fileName] = content;
        _additionalFiles.Add(fileName);
        return this;
    }

    public ProjectBuilder WithSdkVersion(string sdkVersion)
    {
        _globalJsonSdk = sdkVersion;
        return this;
    }

    public async Task<CliResult> BuildAsync(string extraArguments = "")
    {
        Write();
        return await DotnetCli.RunAsync($"build -nologo -v q -clp:NoSummary {extraArguments}", Directory, TestContext.Current.CancellationToken, fixture.PackagesDirectory);
    }

    private void Write()
    {
        System.IO.Directory.CreateDirectory(Directory);

        File.WriteAllText(Path.Combine(Directory, "NuGet.config"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <config>
                <add key="globalPackagesFolder" value="{fixture.PackagesDirectory}" />
              </config>
              <packageSources>
                <clear />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                <add key="local" value="{fixture.FeedDirectory}" />
              </packageSources>
              <packageSourceMapping>
                <packageSource key="nuget.org">
                  <package pattern="*" />
                </packageSource>
                <packageSource key="local">
                  <package pattern="DotNetCodeQuality" />
                </packageSource>
              </packageSourceMapping>
            </configuration>
            """);

        var project = new StringBuilder();
        project.Append($"<Project Sdk=\"{_sdk}\">\n  <PropertyGroup>\n");
        project.Append($"    <OutputType>{_outputType}</OutputType>\n");
        project.Append($"    <TargetFramework>{_targetFramework}</TargetFramework>\n");
        foreach (var (name, value) in _properties)
        {
            project.Append($"    <{name}>{value}</{name}>\n");
        }

        project.Append("  </PropertyGroup>\n  <ItemGroup>\n");
        project.Append($"    <PackageReference Include=\"DotNetCodeQuality\" Version=\"{PackageFixture.PackageVersion}\" PrivateAssets=\"all\" />\n");
        foreach (var (id, version) in _packages)
        {
            project.Append($"    <PackageReference Include=\"{id}\" Version=\"{version}\" />\n");
        }

        foreach (var file in _additionalFiles)
        {
            project.Append($"    <AdditionalFiles Include=\"{file}\" />\n");
        }

        project.Append("  </ItemGroup>\n</Project>\n");
        File.WriteAllText(Path.Combine(Directory, "Sample.csproj"), project.ToString());

        foreach (var (name, content) in _files)
        {
            File.WriteAllText(Path.Combine(Directory, name), content);
        }

        if (_globalJsonSdk is not null)
        {
            File.WriteAllText(Path.Combine(Directory, "global.json"), $$"""{ "sdk": { "version": "{{_globalJsonSdk}}", "rollForward": "latestFeature", "allowPrerelease": true } }""");
        }
    }
}
