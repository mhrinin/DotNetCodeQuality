using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Diagnostics;

var check = args.Contains("--check", StringComparer.Ordinal);
var root = FindRepositoryRoot();
var csprojPath = Path.Combine(root, "src", "DotNetCodeQuality", "DotNetCodeQuality.csproj");
var configPath = Path.Combine(root, "src", "DotNetCodeQuality", "configuration", "Sonar.globalconfig");

var version = ReadSonarVersion(csprojPath);
var assemblyPath = LocateSonarAssembly(version);
var rules = LoadRules(assemblyPath);

var existing = File.Exists(configPath) ? File.ReadAllText(configPath) : string.Empty;
var enabled = ReadEnabledIds(existing);
var missingEnabled = enabled.Where(id => !rules.ContainsKey(id)).ToList();
if (missingEnabled.Count > 0)
{
    Console.Error.WriteLine($"Enabled rules not found in SonarAnalyzer.CSharp {version}: {string.Join(", ", missingEnabled)}");
    return 2;
}

var generated = Render(rules, enabled);

if (check)
{
    if (string.Equals(Normalize(existing), Normalize(generated), StringComparison.Ordinal))
    {
        Console.WriteLine($"Sonar.globalconfig is up to date for SonarAnalyzer.CSharp {version}.");
        return 0;
    }

    var before = IdsIn(existing);
    var after = IdsIn(generated);
    Console.Error.WriteLine($"Sonar.globalconfig is stale for SonarAnalyzer.CSharp {version}.");
    Console.Error.WriteLine($"  new rules:     {string.Join(", ", after.Except(before))}");
    Console.Error.WriteLine($"  removed rules: {string.Join(", ", before.Except(after))}");
    Console.Error.WriteLine("Run `dotnet run --project tools/SonarConfigGenerator` to regenerate.");
    return 1;
}

File.WriteAllText(configPath, generated, new UTF8Encoding(false));
Console.WriteLine($"Wrote {configPath}: {enabled.Count} enabled, {rules.Count(r => r.Value.Enabled) - enabled.Count} switched off (SonarAnalyzer.CSharp {version}).");
return 0;

static string FindRepositoryRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "DotNetCodeQuality", "DotNetCodeQuality.csproj")))
    {
        dir = dir.Parent;
    }

    return dir?.FullName ?? throw new InvalidOperationException("Repository root not found above " + AppContext.BaseDirectory);
}

static string ReadSonarVersion(string csprojPath)
{
    var match = Regex.Match(File.ReadAllText(csprojPath), @"Include=""SonarAnalyzer\.CSharp""\s+Version=""([^""]+)""");
    return match.Success ? match.Groups[1].Value : throw new InvalidOperationException("SonarAnalyzer.CSharp reference not found in " + csprojPath);
}

static string LocateSonarAssembly(string version)
{
    foreach (var packagesRoot in PackageRoots())
    {
        var candidate = Path.Combine(packagesRoot, "sonaranalyzer.csharp", version, "analyzers", "SonarAnalyzer.CSharp.dll");
        if (File.Exists(candidate))
        {
            return candidate;
        }
    }

    throw new FileNotFoundException($"SonarAnalyzer.CSharp {version} is not in the NuGet cache. Run `dotnet restore src/DotNetCodeQuality` first.");
}

static IEnumerable<string> PackageRoots()
{
    if (Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } env)
    {
        yield return env;
    }

    var psi = new ProcessStartInfo("dotnet", "nuget locals global-packages --list") { RedirectStandardOutput = true, UseShellExecute = false };
    using var process = Process.Start(psi);
    if (process is not null)
    {
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        var match = Regex.Match(output, @"global-packages:\s*(.+)");
        if (match.Success)
        {
            yield return match.Groups[1].Value.Trim();
        }
    }

    yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
}

static SortedDictionary<string, (string Title, bool Enabled)> LoadRules(string assemblyPath)
{
    var assembly = Assembly.LoadFrom(assemblyPath);
    Type[] types;
    try
    {
        types = assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException e)
    {
        types = e.Types.Where(t => t is not null).ToArray()!;
    }

    var rules = new SortedDictionary<string, (string Title, bool Enabled)>(StringComparer.Ordinal);
    foreach (var type in types.Where(t => !t.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) is not null))
    {
        DiagnosticAnalyzer analyzer;
        try
        {
            analyzer = (DiagnosticAnalyzer)Activator.CreateInstance(type)!;
        }
        catch (Exception)
        {
            continue;
        }

        foreach (var descriptor in analyzer.SupportedDiagnostics)
        {
            rules[descriptor.Id] = (descriptor.Title.ToString().Replace('"', '\'').Trim(), descriptor.IsEnabledByDefault);
        }
    }

    return rules;
}

static List<string> ReadEnabledIds(string content)
{
    var section = content.Split("# Sonar defaults switched off")[0];
    return Regex.Matches(section, @"dotnet_diagnostic\.(\S+)\.severity = warning").Select(m => m.Groups[1].Value).ToList();
}

static string Render(SortedDictionary<string, (string Title, bool Enabled)> rules, List<string> enabled)
{
    var builder = new StringBuilder();
    builder.Append("is_global = true\n\n# Enabled\n\n");
    foreach (var id in enabled)
    {
        builder.Append($"# {rules[id].Title}\ndotnet_diagnostic.{id}.severity = warning\n\n");
    }

    builder.Append("# Sonar defaults switched off\n\n");
    foreach (var id in rules.Where(r => r.Value.Enabled && !enabled.Contains(r.Key)).Select(r => r.Key).OrderBy(SortKey).ThenBy(id => id, StringComparer.Ordinal))
    {
        builder.Append($"# {rules[id].Title}\ndotnet_diagnostic.{id}.severity = none\n\n");
    }

    return builder.ToString().TrimEnd('\n') + "\n";
}

static int SortKey(string id)
{
    var match = Regex.Match(id, @"^S(\d+)");
    return match.Success ? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : int.MaxValue;
}

static string Normalize(string text) => text.Replace("\r\n", "\n");

static HashSet<string> IdsIn(string content) =>
    Regex.Matches(content, @"dotnet_diagnostic\.(\S+)\.severity").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
