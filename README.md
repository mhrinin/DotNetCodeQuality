# DotNetCodeQuality

**Agentic Code Quality for .NET.**

One `PackageReference` that makes the build enforce code quality: analyzer defaults, style rules, a curated Sonar rule set with thresholds, banned APIs, dependency audit, and project-type profiles. Warnings are errors by default.

## Why

Coding agents do not read your standards document. They run `dotnet build` and read the output. The only rules that reliably shape generated code are the ones that turn the build red, and the same rules keep human contributions consistent. This package puts those rules in a single, versioned dependency instead of files copied from repo to repo.

## Install

```xml
<!-- Directory.Build.props at the repository root -->
<Project>
  <ItemGroup>
    <PackageReference Include="DotNetCodeQuality" Version="1.0.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

Build. Fix what fails. That is the whole adoption procedure. For a large existing codebase, start with `<DotNetCodeQualityStrict>false</DotNetCodeQualityStrict>` to see the volume before enforcing.

## What you get

| Area | Content |
| --- | --- |
| Compiler defaults | `Nullable`, `ImplicitUsings`, `AnalysisLevel=latest`, `AnalysisMode=Recommended`, `EnforceCodeStyleInBuild`, documentation file (CS1591 silenced), deterministic builds on CI |
| Strict build | `TreatWarningsAsErrors`, NuGet vulnerability audit (NU1901-NU1904) as errors |
| Style | braces, expression-bodied members, switch and collection expressions, primary constructors, file-scoped namespaces, `var` when apparent, target-typed `new`, using hygiene, null patterns, naming conventions, unused private members / parameters / assignments |
| Analysis tuning | rules that fight real codebases are off: identifiers matching keywords or type names (CA1716, CA1720), `GC.SuppressFinalize` (CA1816), LoggerMessage delegates (CA1848, CA1873), indexer-over-LINQ micro-optimisation (CA1826) |
| Sonar | SonarAnalyzer.CSharp with 7 rules on (cognitive complexity, method length, parameter count, unused private members / locals / parameters / fields) and every other default rule off; thresholds: 100 lines per method, 10 parameters |
| Banned APIs | `DateTime.Now`, `DateTimeOffset.Now`, `Task.Result`, `Task.Wait()`, `Thread.Sleep` |
| Profiles | `App` (default), `Test` (relaxes naming, constant arrays, parameter count; detected automatically), `Library` (adds `ConfigureAwait(false)`; opt in per project) |

## Properties

Set any of these in a `.csproj` or `Directory.Build.props`.

| Property | Default | Meaning |
| --- | --- | --- |
| `DotNetCodeQualityStrict` | `true` | warnings as errors, audit findings as errors |
| `DotNetCodeQualityProfile` | `App`, or `Test` when detected | `App`, `Library` or `Test`; an explicit value wins over detection |
| `DotNetCodeQualitySonar` | `true` | `false` removes the Sonar analyzer entirely |
| `DotNetCodeQualitySonarLintXml` | package file | path to your own `SonarLint.xml` with rule thresholds |
| `DotNetCodeQualityBannedSymbols` | `true` | include the starter `BannedSymbols.txt`; your own `BannedSymbols.txt` files are additive |
| `DotNetCodeQualityStyle` | `true` | include the style rules |

Every standard MSBuild property the package sets uses a `Condition="'$(X)' == ''"` guard, so a value in your project always wins. The one exception is `TreatWarningsAsErrors`, which strict mode sets outright; use `DotNetCodeQualityStrict` to control it.

NuGet audit findings (NU1901-NU1904) fail the build through their replay at build time. The audit mode and level the package sets take effect from the second restore onwards, because a package's settings are not yet imported during the restore that first brings it in.

Profile detection: `Test` when the project references Microsoft.NET.Test.Sdk, xunit, NUnit or MSTest; `App` otherwise. `Library` is never inferred, because most class libraries in an application repository are internal plumbing. Declare it on libraries that are genuinely reused elsewhere:

```xml
<DotNetCodeQualityProfile>Library</DotNetCodeQualityProfile>
```

## What stays in your repository

Nothing is required. Keep a `.editorconfig` only for editor formatting and for exceptions scoped to a folder, which a global config cannot express:

```ini
[src/Data/Migrations/*.cs]
generated_code = true

[src/Web/Features/Seeding/*.cs]
dotnet_diagnostic.S3776.severity = none
```

## Precedence

The package ships its rules as `.globalconfig` files at `global_level = 100`. They override the SDK's built-in analysis levels (`-100`) and lose to your `.editorconfig`, so any rule can be adjusted per repository or per folder. If you prefer your own `.globalconfig`, give it a `global_level` above 100.

## License

MIT
