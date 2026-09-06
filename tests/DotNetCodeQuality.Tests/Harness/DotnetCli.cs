using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace DotNetCodeQuality.Tests.Harness;

public sealed record CliResult(int ExitCode, string Output)
{
    private static readonly Regex DiagnosticPattern = new(@"\b(error|warning) ([A-Z]+[0-9]+)\b", RegexOptions.Compiled);

    public IReadOnlyList<(string Id, string Level)> Diagnostics =>
        DiagnosticPattern.Matches(Output).Select(m => (Id: m.Groups[2].Value, Level: m.Groups[1].Value)).Distinct().ToList();

    public bool HasError(string id) => Diagnostics.Contains((id, "error"));

    public bool HasWarning(string id) => Diagnostics.Contains((id, "warning"));

    public bool Has(string id) => Diagnostics.Any(d => d.Id == id);

    public IEnumerable<string> IdsStartingWith(string prefix) => Diagnostics.Select(d => d.Id).Where(id => id.StartsWith(prefix, StringComparison.Ordinal)).Distinct();
}

public static class DotnetCli
{
    public static async Task<CliResult> RunAsync(string arguments, string workingDirectory, CancellationToken cancellationToken, string? packagesDirectory = null)
    {
        var startInfo = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var key in startInfo.Environment.Keys.Where(IsBuildHostVariable).ToList())
        {
            startInfo.Environment.Remove(key);
        }

        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        startInfo.Environment["MSBUILDTERMINALLOGGER"] = "off";
        if (packagesDirectory is not null)
        {
            startInfo.Environment["NUGET_PACKAGES"] = packagesDirectory;
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("dotnet could not be started");
        var output = new StringBuilder();
        var stdout = PumpAsync(process.StandardOutput, output);
        var stderr = PumpAsync(process.StandardError, output);
        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(stdout, stderr);
        return new CliResult(process.ExitCode, output.ToString());
    }

    private static bool IsBuildHostVariable(string name) =>
        name.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)
        || name.Equals("DOTNET_HOST_PATH", StringComparison.OrdinalIgnoreCase)
        || name.Equals("DOTNET_ROOT", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("DOTNET_ROOT_", StringComparison.OrdinalIgnoreCase)
        || name.Equals("DOTNET_STARTUP_HOOKS", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("TESTINGPLATFORM_", StringComparison.OrdinalIgnoreCase);

    private static async Task PumpAsync(StreamReader reader, StringBuilder target)
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            lock (target)
            {
                target.AppendLine(line);
            }
        }
    }
}
