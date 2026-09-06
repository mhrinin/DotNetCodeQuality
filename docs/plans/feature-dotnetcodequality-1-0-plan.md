# DotNetCodeQuality 1.0

Build a public, MIT-licensed NuGet package `DotNetCodeQuality` ("Agentic Code Quality for .NET", repo `mhrinin/DotNetCodeQuality`) that gives any .NET project build-enforced code quality through one `PackageReference`: compiler/analysis defaults, style rules, curated Sonar rules with thresholds, banned APIs, dependency audit as errors, and project-type profiles. Then adopt it in the Israel Museum repo, replacing the inline files created this week.

## For Future Agents
As work proceeds: mark checkboxes `- [x]` as items complete; when a phase is done, set its status to `Complete` and write its **Phase Summary** (what was done, key decisions, anything needed to continue with zero context); run the phase's **Verification Plan** and record the result before moving on. When all phases are done, fill in **Final Recap** and **Deployment Plan**. If any phase reveals the plan is wrong, contradictory, or impossible, STOP and surface the conflict — silent rescoping is not allowed; the plan is updated through an explicit revision, not absorbed into a phase.

## Summary

**Type:** feature (new public repo + NuGet package, then cross-repo adoption).

**Context.** The article "How to make AI write high-quality code in .NET" argues that agents read build output, not standards documents, so quality rules must fail the build. We applied that to the museum repo (2026-09-06): Directory.Build.props analysis settings, `Sonar.globalconfig` with 7 curated rules on and 322 defaults off, `SonarLint.xml` thresholds, `.editorconfig` style enforcement, ~215 violations cleaned. The team's `D:\Projects\claude_rules\dotnet` repo distributes the same ideas by file copy, and both copies in the museum repo had already drifted. The user wants one portable thing: no copy procedure, no init tool, no skill. We verified empirically (2026-09-06) that code-style options and IDE severities work from a `.globalconfig` hooked via `GlobalAnalyzerConfigFiles`, so a props/targets NuGet package can carry everything. A consuming repo keeps its own `.editorconfig` only for folder-scoped exceptions and editor formatting.

**Decisions taken (do not reopen):**
- Name `DotNetCodeQuality` (PascalCase `DotNet`, Microsoft convention). Tagline "Agentic Code Quality for .NET". MIT. Public repo `mhrinin/DotNetCodeQuality`.
- Single package. `SonarAnalyzer.CSharp` and `Microsoft.CodeAnalysis.BannedApiAnalyzers` are package dependencies.
- Strict (warnings as errors) by default in every build, including local Debug; opt out with `DotNetCodeQualityStrict=false`.
- Profiles App (default) / Library / Test, auto-detected, overridable. Library = App + CA2007 ConfigureAwait(false). CA1515 (make types internal) nowhere.
- Banned APIs starter list: `DateTime.Now`, `DateTimeOffset.Now`, `Task<T>.Result`, `Task.Wait()`, `TaskAwaiter.GetResult()`, `Thread.Sleep`.
- Publishing: tag-driven (`v*`), MinVer, GitHub Actions to nuget.org.
- SDK floor: .NET 6 SDK and newer (6, 8, 9, 10 installed locally: 6.0.304, 8.0.100-rc.1, 9.0.315, 10.0.400).
- Museum adoption is the final phase of this plan.

## Goals and success criteria
1. A fresh console project with only `<PackageReference Include="DotNetCodeQuality" />` fails its Debug build on: an unused local (S1481), a brace-less `if` (IDE0011), `DateTime.Now` (RS0030), a block-scoped namespace (IDE0161).
2. The same project with `DotNetCodeQualityStrict=false` builds green and reports those as warnings.
3. A consumer `.editorconfig` line overrides any rule the package sets (package config loses to repo config).
4. Sonar's 322 default rules do not fire (e.g. no S6562 on `new DateTime(...)`), and `DotNetCodeQualitySonar=false` removes every S-diagnostic.
5. Test projects (xunit v3 MTP, or Microsoft.NET.Test.Sdk) are detected and get CA1707/CA1861/S107 relaxed; libraries get CA2007; web apps do not.
6. Thresholds hold: 10 parameters pass, 11 fail (S107); 100-line method passes, 101 fails (S138).
7. The package restores and builds green on SDK 6, 8, 9 and 10.
8. `git tag v1.0.0` + push publishes `DotNetCodeQuality 1.0.0` to nuget.org via GitHub Actions.
9. The museum repo builds green with one PackageReference replacing `Sonar.globalconfig`, `SonarLint.xml`, the analysis block in `Directory.Build.props` and the rule lines in `.editorconfig`; its 564 tests pass.

## Architecture integration

**Layout (mirrors Meziantou.DotNet.CodingStandard, verified from the 1.0.181 nupkg):**
```
DotNetCodeQuality/
  README.md  LICENSE  .gitignore  .editorconfig  global.json  DotNetCodeQuality.slnx
  docs/plans/feature-dotnetcodequality-1-0-plan.md   (this plan)
  src/DotNetCodeQuality/
    DotNetCodeQuality.csproj                 props-only package project
    build/DotNetCodeQuality.props            defaults (evaluated before the project body)
    build/DotNetCodeQuality.targets          profile detection, config/item wiring, opt-out targets
    buildTransitive/DotNetCodeQuality.{props,targets}      3-line shims importing ../build/*
    buildMultiTargeting/DotNetCodeQuality.{props,targets}  3-line shims importing ../build/*
    configuration/Analysis.globalconfig      CA adjustments (CA1716/1720/1816/1848/1873 none) + IDE0051/0052/0059/0060 warning
    configuration/Style.globalconfig         style + naming rules (from claude_rules/dotnet/.editorconfig)
    configuration/Sonar.globalconfig         7 on, all other default-enabled Sonar rules none (generated, annotated)
    configuration/Profile.Library.globalconfig   CA2007 warning
    configuration/Profile.Test.globalconfig      CA1707, CA1861, S107 none
    configuration/SonarLint.xml              S138 max=100, S107 max=10
    configuration/BannedSymbols.txt          starter bans
  tools/SonarConfigGenerator/                regenerates Sonar.globalconfig from the referenced Sonar version; --check mode
  tests/DotNetCodeQuality.Tests/             xunit; packs the package, builds throwaway projects, asserts on SARIF
  .github/workflows/ci.yml  release.yml
```

**Precedence model (verified 2026-09-06):** the SDK's `analysislevel_*.globalconfig` files declare `global_level = -100`; a file named `*.globalconfig` defaults to `global_level = 100`; `.editorconfig` beats every global config. So package files override SDK defaults and lose to a consumer's `.editorconfig`. Document: a consumer who prefers a `.globalconfig` must set `global_level` above 100.

**Sources to lift from (museum repo, current working tree):** `Directory.Build.props` (analysis properties, Sonar wiring), `Sonar.globalconfig` (annotated list generated from SonarAnalyzer.CSharp 10.33.0.1635), `SonarLint.xml`; `D:\Projects\claude_rules\dotnet\.editorconfig` for style/naming; the museum `.editorconfig` for the CA adjustments and IDE unused-code rules. The reflection approach for enumerating Sonar rules (console app referencing `Microsoft.CodeAnalysis.CSharp` + `Microsoft.CodeAnalysis.Workspaces.Common` 5.0.0, catching `ReflectionTypeLoadException`) becomes `tools/SonarConfigGenerator`.

**Package facts verified from nuget.org:** SonarAnalyzer.CSharp 10.33.0.1635 ships one DLL at `analyzers/SonarAnalyzer.CSharp.dll` (no Roslyn-versioned folder), `developmentDependency=true`, no dependencies. Microsoft.CodeAnalysis.BannedApiAnalyzers latest stable 5.6.0, `analyzers/dotnet/cs/`. MinVer latest 8.0.0. xunit.v3 sets `TestProject=true` and `XunitTestProject=true` (not `IsTestProject`); Microsoft.NET.Test.Sdk sets `IsTestProject=true` and `TestProject=true`.

**MSBuild property surface:**

| Property | Default | Meaning |
| --- | --- | --- |
| `DotNetCodeQualityStrict` | `true` | TreatWarningsAsErrors + NU1901–NU1904 in WarningsAsErrors |
| `DotNetCodeQualityProfile` | auto (`App`/`Library`/`Test`) | which `Profile.*.globalconfig` is added; explicit value wins over detection |
| `DotNetCodeQualitySonar` | `true` | `false` removes the Sonar analyzer from `@(Analyzer)` and skips Sonar config |
| `DotNetCodeQualitySonarLintXml` | package `SonarLint.xml` | path of the thresholds file; consumers point it at their own |
| `DotNetCodeQualityBannedSymbols` | `true` | adds the starter `BannedSymbols.txt`; consumer files are additive |
| `DotNetCodeQualityStyle` | `true` | adds `Style.globalconfig` |
| Standard MSBuild | set only when empty | `Nullable`, `ImplicitUsings`, `AnalysisLevel=latest`, `AnalysisMode=Recommended`, `EnforceCodeStyleInBuild`, `GenerateDocumentationFile` (+CS1591 in NoWarn), `NuGetAudit`/`NuGetAuditMode=all`/`NuGetAuditLevel=low`, `Deterministic`, `ContinuousIntegrationBuild` from CI env vars |

**Profile detection (in `.targets`, after the project body):** explicit `DotNetCodeQualityProfile` → else `Test` when `IsTestProject`/`TestProject`/`XunitTestProject` is `true` or a `PackageReference` matches `xunit*`, `NUnit`, `MSTest.TestFramework` → else `Library` when `OutputType` is `Library` and `UsingMicrosoftNETSdkWeb` is not `true` → else `App`.

## Phase 1: Repository bootstrap
Status: Complete
Out of scope for this phase: any MSBuild content.

- [x] `git init` in `D:\Projects\DotNetCodeQuality` (the folder already holds `docs/plans/` with this plan)
- [x] Add `README.md` (title, tagline "Agentic Code Quality for .NET", one paragraph on why build-enforced rules matter for agents, install snippet, property table, "what stays in your repo" section, precedence note), `LICENSE` (MIT, copyright Mykhailo Hrinin), `.gitignore` (dotnet), `.editorconfig` (formatting only: indent, charset, final newline), `global.json` pinning the 10.0.x SDK with `rollForward: latestFeature`
- [x] `gh repo create mhrinin/DotNetCodeQuality --public --description "Agentic Code Quality for .NET"`; first commit and push; enable Actions

### Acceptance criteria
1. `https://github.com/mhrinin/DotNetCodeQuality` is public, contains the README, license and this plan.

### Verification Plan
- `gh repo view mhrinin/DotNetCodeQuality --json visibility,description` → `PUBLIC`, tagline present.

### Phase Summary
Repo `mhrinin/DotNetCodeQuality` created public on 2026-09-06 with README (tagline, install, property table, precedence), MIT license, dotnet .gitignore, formatting-only .editorconfig, global.json (10.0.100, latestFeature) and this plan. Verified: `gh repo view` reports PUBLIC with the tagline.

## Phase 2: Package project, props, targets, configuration
Status: Complete
Out of scope for this phase: tests, generator tool, CI, publishing.

- [x] `src/DotNetCodeQuality/DotNetCodeQuality.csproj`: `TargetFramework=netstandard2.0`, `IncludeBuildOutput=false`, `DevelopmentDependency=true`, `NoWarn` NU5128, `PackageId/Authors/Description/PackageLicenseExpression=MIT/PackageReadmeFile/RepositoryUrl/PackageTags`; `PackageReference` to `SonarAnalyzer.CSharp` 10.33.0.1635 and `Microsoft.CodeAnalysis.BannedApiAnalyzers` 5.6.0 with `PrivateAssets="none"` and `ExcludeAssets="all"` so they appear as nuspec dependencies without running in the package project; `MinVer` 8.0.0 with `PrivateAssets="all"`, `MinVerTagPrefix=v`; `None` items packing `build/**`, `buildTransitive/**`, `buildMultiTargeting/**`, `configuration/**` to the package root
- [x] `build/DotNetCodeQuality.props`: every default guarded by `Condition="'$(X)' == ''"`; `DotNetCodeQuality*` property defaults; strict block (`TreatWarningsAsErrors`, `WarningsAsErrors` += NU1901;NU1902;NU1903;NU1904) conditioned on `DotNetCodeQualityStrict == true`; `NoWarn` += CS1591; CI detection matrix (`GITHUB_ACTIONS`, `TF_BUILD`, `CI`, `GITLAB_CI`, `TEAMCITY_VERSION`, `BITBUCKET_BUILD_NUMBER`) → `ContinuousIntegrationBuild=true`
- [x] `build/DotNetCodeQuality.targets`: profile detection property group; `GlobalAnalyzerConfigFiles` items for `Analysis`, `Style` (when Style true), `Sonar` (when Sonar true), `Profile.$(DotNetCodeQualityProfile)`; `AdditionalFiles` for `$(DotNetCodeQualitySonarLintXml)` (when Sonar true) and `BannedSymbols.txt` (when BannedSymbols true), all `Visible="false"`; target `DotNetCodeQualityRemoveSonar` `BeforeTargets="CoreCompile"` with `<Analyzer Remove="@(Analyzer)" Condition="'%(Filename)' == 'SonarAnalyzer.CSharp'" />` when Sonar false
- [x] Shims in `buildTransitive/` and `buildMultiTargeting/`
- [x] `configuration/Analysis.globalconfig`, `Style.globalconfig` (all rules from `claude_rules/dotnet/.editorconfig`, no section headers), `Sonar.globalconfig` (copy from museum), `Profile.Library.globalconfig`, `Profile.Test.globalconfig`, `SonarLint.xml` (copy), `BannedSymbols.txt` (six entries with messages: use `TimeProvider`/`UtcNow`; use `await`; use `Task.Delay`)
- [x] `dotnet pack -c Release -o artifacts` locally; inspect the nupkg listing and nuspec

### Acceptance criteria
1. The nuspec lists exactly two dependencies (Sonar, BannedApi) and `developmentDependency=true`; the package has no `lib/` folder.
2. A scratch console project outside the repo, restoring from the `artifacts` folder via a NuGet.config source, fails on S1481, IDE0011, IDE0161 and RS0030 and passes when the code is fixed.

### Tests first
- Deferred to Phase 4, which builds the harness; Phase 2 is verified manually with the scratch project.

### Verification Plan
- `dotnet pack src/DotNetCodeQuality -c Release -o artifacts` → one `.nupkg`; `unzip -l` shows `build/`, `buildTransitive/`, `buildMultiTargeting/`, `configuration/`, no `lib/`.
- Scratch project build with a `DateTime.Now`: `dotnet build` exit 1 with `error RS0030`; with `-p:DotNetCodeQualityStrict=false`: exit 0 with `warning RS0030`.

### Phase Summary
Package project, props, targets, shims and eight configuration files written. Two deviations from the plan, both forced by MSBuild behaviour discovered in testing:
- Dependencies are declared with `PrivateAssets="none"` only. `ExcludeAssets="all"` removed them from the nuspec entirely. The Sonar and BannedApi analyzers therefore also run inside the package project, which has no compile items, so this is harmless.
- Config files are added as `EditorConfigFiles`, not `GlobalAnalyzerConfigFiles`. The SDK converts `GlobalAnalyzerConfigFiles` to compiler inputs in a static ItemGroup in Microsoft.Managed.Core.targets, which is evaluated before a NuGet package's .targets is imported, so package-added items never reach csc. `EditorConfigFiles` is read by the Csc task at execution time and works from both static items and the profile target. Meziantou uses the same item.
- Test-package detection uses `String.Contains` on a `;`-joined lower-cased `@(PackageReference)` list; MSBuild rejects a Regex property function whose pattern contains `(`, `)` or `,`.
- The configuration directory is normalised with `System.IO.Path.GetFullPath` so paths contain no `..`.
Verified with a scratch console consumer restoring 999.9.9 from `artifacts/`: strict build fails with CS0219, IDE0011, IDE0059, IDE0161, RS0030, S1481; `DotNetCodeQualityStrict=false` reports the same as warnings and exits 0; `new DateTime(2020,1,1)` raises no S6562, proving the Sonar off-list is applied; the nuspec lists the two dependencies with `developmentDependency=true` and the package has no `lib/`.

## Phase 3: Sonar config generator
Status: Complete
Out of scope for this phase: generating configs for any other analyzer.

- [x] `tools/SonarConfigGenerator` console project (net10.0): references `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.Workspaces.Common` 5.0.0; locates `SonarAnalyzer.CSharp.dll` in the NuGet cache for the version referenced by `src/DotNetCodeQuality/DotNetCodeQuality.csproj` (read the csproj); enumerates `DiagnosticAnalyzer.SupportedDiagnostics` tolerating `ReflectionTypeLoadException`
- [x] Reads the enabled list from the existing `Sonar.globalconfig` (the `# Enabled` section), rewrites the file: header, `# Enabled` entries as `warning`, `# Sonar defaults switched off` with every other default-enabled rule as `none`, each preceded by its Sonar title comment
- [x] `--check` mode: exit 1 with a diff summary when the committed file differs from the generated one
- [ ] Wire `dotnet run --project tools/SonarConfigGenerator -- --check` into the CI job (Phase 5)

### Acceptance criteria
1. Running the generator on the current Sonar version reproduces the committed file byte for byte.
2. Editing the csproj to a different Sonar version and running `--check` fails with the list of new/removed rule ids.

### Tests first
- xunit test in Phase 4 project: generator on the referenced version → `--check` exits 0.

### Verification Plan
- `dotnet run --project tools/SonarConfigGenerator -- --check` → exit 0, "Sonar.globalconfig is up to date".

### Phase Summary
`tools/SonarConfigGenerator` (net10.0 console, references Microsoft.CodeAnalysis.CSharp + Workspaces.Common 5.0.0) reads the Sonar version from the package csproj, finds the DLL in the NuGet cache (NUGET_PACKAGES, `dotnet nuget locals`, or ~/.nuget/packages), enumerates analyzers tolerating ReflectionTypeLoadException, keeps the `# Enabled` list from the committed file and regenerates the off-list with titles. Verified: `--check` exits 0 on the committed file; deleting one rule makes `--check` exit 1 naming S6562 as new; plain run reproduces the committed file with no git diff. CI wiring is a Phase 5 checkbox.

## Phase 4: Package test harness
Status: Complete
Out of scope for this phase: CI wiring (Phase 5).

- [x] `tests/DotNetCodeQuality.Tests` (xunit.v3, Microsoft.NET.Test.Sdk, `IsTestProject=true` explicitly, `DotNetCodeQualityProfile` irrelevant because the test project does not reference the package)
- [x] `PackageFixture`: packs `src/DotNetCodeQuality` once per run into a temp folder with version `999.9.9` (or reuses `$(NuGetDirectory)` on CI)
- [x] `ProjectBuilder`: writes a throwaway project into a temp dir with `NuGet.config` (packageSourceMapping: `*` → nuget.org, `DotNetCodeQuality` → temp folder; isolated `globalPackagesFolder`), optional `global.json` for an SDK version, `<ErrorLog>build.sarif,version=2.1</ErrorLog>`, arbitrary properties, extra package references, source files, optional `.editorconfig` and `BannedSymbols.txt`; runs `dotnet build`, parses SARIF into rule id → level
- [x] Tests (one per line):
  - strict by default: unused local → error S1481; brace-less if → error IDE0011; block namespace → error IDE0161; `DateTime.Now` → error RS0030
  - `DotNetCodeQualityStrict=false` → same ids as warnings, exit 0
  - `new DateTime(2020,1,1)` → no S6562 (Sonar defaults are off)
  - consumer `.editorconfig` with `dotnet_diagnostic.IDE0011.severity = none` → IDE0011 absent
  - `DotNetCodeQualitySonar=false` → no diagnostic id starting with `S`
  - `DotNetCodeQualityBannedSymbols=false` → no RS0030; consumer `BannedSymbols.txt` banning `Console.WriteLine` → RS0030 for it and for `DateTime.Now`
  - xunit.v3 project with method `Foo_Bar` → no CA1707; same with `DotNetCodeQualityProfile=App` → CA1707 error
  - Microsoft.NET.Test.Sdk + xunit v2 project → CA1707 absent
  - class library with `await Task.Delay(1)` → CA2007 error; `Microsoft.NET.Sdk.Web` project with the same → no CA2007; console app → no CA2007
  - method with 10 parameters → no S107; 11 → S107 error; 100-line method → no S138; 101 → S138 error
  - public undocumented class → no CS1591
  - package reference to a version with a known advisory (e.g. `System.Text.Json` 8.0.0) → NU1903 error when strict (SDK ≥ 8 only)
  - SDK matrix: the "strict by default" project builds and reports the same ids under `global.json` for 6.0, 8.0, 9.0 and 10.0 (skip a version when its SDK is not installed)
  - nuspec assertions: two dependencies, `developmentDependency=true`, no `lib/`
  - generator `--check` exits 0

### Acceptance criteria
1. `dotnet test tests/DotNetCodeQuality.Tests` passes locally on this machine (all four SDKs installed).

### Tests first
- This phase is the tests.

### Verification Plan
- `dotnet test tests/DotNetCodeQuality.Tests -nologo` → `Passed!`, 0 failed.

### Phase Summary
`tests/DotNetCodeQuality.Tests` (xunit.v3 3.2.2, Microsoft.NET.Test.Sdk 18.0.1): `PackageFixture` packs 999.9.9 into a per-run temp feed (or copies `DOTNETCODEQUALITY_NUPKG`), `ProjectBuilder` writes throwaway projects with NuGet.config source mapping and an isolated packages folder, `DotnetCli` runs `dotnet` with build-host env vars scrubbed and `NUGET_PACKAGES` pointed at the isolated folder (a machine-wide `NUGET_PACKAGES` otherwise overrides NuGet.config), diagnostics parsed from console output. 28 tests; 27 pass locally, the 8.0 matrix case skips because only 8.0.100-rc.1 is installed here.

Package changes forced by the tests:
- Strict block and the `DotNetCodeQuality*` defaults moved from .props to .targets so a value in the consumer's csproj body is seen. The SDK itself defaults `TreatWarningsAsErrors` to `false` before package targets load, so strict mode sets `TreatWarningsAsErrors` and `CodeAnalysisTreatWarningsAsErrors` outright instead of guarding on empty.
- NU1901-NU1904 are added to `MSBuildWarningsAsErrors` as well as `WarningsAsErrors`; audit findings are restore warnings replayed by ResolvePackageAssets at build time, and only MSBuild-level warnings-as-errors promotes the replay.
- Microsoft.CodeAnalysis.BannedApiAnalyzers pinned to 3.3.4: 5.6.0 fails to load on the .NET 6 SDK compiler (CS8032), which strict mode turned into a build break.
- The `GetAwaiter().GetResult()` bans are dropped: xunit.v3's generated entry point uses that idiom, so every xunit.v3 consumer would have failed.

## Phase 5: CI and first release
Status: Not started
Out of scope for this phase: museum adoption.

- [ ] `.github/workflows/ci.yml`: on push and PR; `actions/setup-dotnet` with `6.0.x`, `8.0.x`, `9.0.x`, `10.0.x`; `dotnet pack` → `NuGetDirectory`; `dotnet test`; generator `--check`; upload nupkg artifact
- [ ] `.github/workflows/release.yml`: on tag `v*`; `dotnet pack` (MinVer derives the version from the tag); `dotnet nuget push --source https://api.nuget.org/v3/index.json --api-key ${{ secrets.NUGET_API_KEY }} --skip-duplicate`
- [ ] User action: create a nuget.org API key scoped to `DotNetCodeQuality` push and store it as repo secret `NUGET_API_KEY`
- [ ] Tag `v1.0.0`, push, confirm the package page on nuget.org shows README, MIT, two dependencies

### Acceptance criteria
1. CI is green on `main` across the SDK matrix.
2. `DotNetCodeQuality 1.0.0` is installable from nuget.org.

### Verification Plan
- `gh run list --workflow ci.yml --limit 1` → `completed success`.
- `curl https://api.nuget.org/v3-flatcontainer/dotnetcodequality/index.json` lists `1.0.0`.

### Phase Summary
_(write when phase completes)_

## Phase 6: Adopt in the Israel Museum repo
Status: Not started
Out of scope for this phase: any rule changes beyond what the package brings; changes to `claude_rules`.

Precondition: the inline-files change currently uncommitted in the museum repo is reviewed and committed first (branch, PR), so this phase is a clean diff on top.

- [ ] `Directory.Build.props`: keep `UmbracoVersion`; add `<PackageReference Include="DotNetCodeQuality" Version="1.0.0" />`; remove the analysis property block, the test NoWarn group, the `GlobalAnalyzerConfigFiles`/`AdditionalFiles` items, the Sonar and SecurityCodeScan package references
- [ ] Delete `Sonar.globalconfig` and `SonarLint.xml`
- [ ] `.editorconfig`: remove every rule the package now carries (style, naming, CA adjustments, IDE unused rules, SCS0005); keep `root = true`, the migrations section (`generated_code = true`, S138 none), the ContentSeeding section, the view-component section
- [ ] `ETL/Directory.Build.props`: import the parent and set `DotNetCodeQualityProfile=App` for non-test projects (`Condition="!$(MSBuildProjectName.EndsWith('.Tests'))"`), because the ETL class libraries are internal to the web app
- [ ] Ideo.* and Seo libraries: add `ConfigureAwait(false)` at the ~23 `await` sites (ExtendedContentPicker 5, Lucene.Search 6, Seo.Robots 2, Seo.Sitemap 10) to satisfy the Library profile
- [ ] Fix anything else the package surfaces (expected: nothing, since the rule sets are identical)
- [ ] Full build, all tests, Release build of the web app

### Acceptance criteria
1. `dotnet build TheIsraelMuseum.sln --no-incremental` → 0 warnings, 0 errors, with warnings-as-errors coming from the package.
2. `dotnet test TheIsraelMuseum.sln` → 564 passed.
3. `git diff --stat` shows the three inline files gone and `.editorconfig` reduced to header + three sections.

### Verification Plan
- `dotnet build TheIsraelMuseum.sln --no-incremental -nologo -v q` → exit 0, no `warning`/`error` lines.
- `dotnet test TheIsraelMuseum.sln --no-build` → four `Passed!` lines.
- `dotnet build TheIsraelMuseum.Web -c Release --no-incremental` → exit 0.

### Phase Summary
_(write when phase completes)_

## New dependencies or infrastructure
- nuget.org account for `mhrinin` and an API key stored as GitHub secret `NUGET_API_KEY` (user action).
- Package dependencies: SonarAnalyzer.CSharp 10.33.0.1635, Microsoft.CodeAnalysis.BannedApiAnalyzers 5.6.0. Build-time only: MinVer 8.0.0.
- Museum repo: SecurityCodeScan.VS2019 is dropped on adoption; Microsoft's built-in CA security rules remain.

## Edge cases and risks
- **Old SDK compatibility.** SonarAnalyzer 10.x on the .NET 6 SDK's Roslyn is unverified. If the analyzer fails to load, the compiler emits CS8032 as a warning, which strict mode turns into an error. Phase 4's SDK-matrix test detects this; mitigation options are documenting a higher floor for Sonar or adding CS8032 to `WarningsNotAsErrors` on old SDKs.
- **Sonar version bumps** enable new default rules in every consumer. The generator plus its CI `--check` makes a stale off-list a failing build in this repo before release.
- **Two `SonarLint.xml` files** in one compilation is ambiguous; the `DotNetCodeQualitySonarLintXml` property exists so consumers replace rather than add.
- **Consumer `.globalconfig` at level 100** conflicts with the package files (Roslyn warns on equal-level conflicts). README documents raising `global_level`.
- **`NuGetAudit*` properties** are unknown to SDK 6/7 and ignored there; the audit test is SDK ≥ 8 only.
- **Museum ETL tests**: the ETL folder pin to `App` must not catch the ETL test project, hence the name condition.
- **MinVer on a fresh repo** yields `0.0.0-alpha` until the first tag; CI packs succeed regardless.

## Out of scope (work-wide)
- Meziantou.Analyzer, Roslynator, Microsoft.VisualStudio.Threading.Analyzers, StyleCop.
- Central Package Management.
- An init tool, `dotnet new` template, or Claude skill.
- Updating `D:\Projects\claude_rules\dotnet` to point at the package (separate follow-up in that repo).
- Rider live-feedback verification for globalconfig files (build enforcement is the requirement).

## Open questions
- None blocking. Assumption: `PrivateAssets="none" ExcludeAssets="all"` on the analyzer PackageReferences yields nuspec dependencies without running the analyzers in the package project; Phase 2's nuspec inspection confirms or corrects this.

## Final Recap
_(write when all phases complete)_

## Deployment Plan
_(write when all phases complete)_
