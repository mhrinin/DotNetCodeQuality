using DotNetCodeQuality.Tests.Harness;

namespace DotNetCodeQuality.Tests;

[Collection(nameof(PackageCollection))]
public sealed class ThresholdTests(PackageFixture fixture)
{
    public sealed record Threshold(string Property, string Rule, int Default, int Override, int Slack, Func<int, string> Program)
    {
        public override string ToString() => Property;
    }

    public static TheoryData<Threshold> Thresholds =>
    [
        new Threshold("DotNetCodeQualityMaxMethodLines", "S138", Default: 100, Override: 30, Slack: 10, MethodWithStatements),
        new Threshold("DotNetCodeQualityMaxParameters", "S107", Default: 10, Override: 3, Slack: 0, MethodWithParameters),
        new Threshold("DotNetCodeQualityMaxCognitiveComplexity", "S3776", Default: 15, Override: 5, Slack: 0, MethodWithSequentialIfs),
        new Threshold("DotNetCodeQualityMaxClassCoupling", "S1200", Default: 20, Override: 5, Slack: 0, ClassCoupledTo),
        new Threshold("DotNetCodeQualityMaxInheritanceDepth", "S110", Default: 5, Override: 2, Slack: 1, InheritanceChain),
        new Threshold("DotNetCodeQualityMaxFileLines", "S104", Default: 1000, Override: 100, Slack: 10, FileWithLines),
        new Threshold("DotNetCodeQualityMaxNestingDepth", "S134", Default: 3, Override: 1, Slack: 0, NestedIfs),
    ];

    [Theory]
    [MemberData(nameof(Thresholds))]
    public async Task Default_ReportsOnlyAboveThreshold(Threshold threshold)
    {
        var below = await new ProjectBuilder(fixture).WithSource("Program.cs", threshold.Program(threshold.Default - threshold.Slack)).BuildAsync();
        var above = await new ProjectBuilder(fixture).WithSource("Program.cs", threshold.Program(threshold.Default + 1 + threshold.Slack)).BuildAsync();

        Assert.False(below.Has(threshold.Rule), below.Output);
        Assert.True(above.HasError(threshold.Rule), above.Output);
    }

    [Theory]
    [MemberData(nameof(Thresholds))]
    public async Task Property_MovesThreshold(Threshold threshold)
    {
        var below = await new ProjectBuilder(fixture)
            .WithProperty(threshold.Property, threshold.Override.ToString())
            .WithSource("Program.cs", threshold.Program(threshold.Override - threshold.Slack))
            .BuildAsync();
        var above = await new ProjectBuilder(fixture)
            .WithProperty(threshold.Property, threshold.Override.ToString())
            .WithSource("Program.cs", threshold.Program(threshold.Override + 1 + threshold.Slack))
            .BuildAsync();

        Assert.False(below.Has(threshold.Rule), below.Output);
        Assert.True(above.HasError(threshold.Rule), above.Output);
    }

    [Fact]
    public async Task ConsumerSonarLintXml_ReplacesGeneratedThresholds()
    {
        const string sonarLint = """
            <?xml version="1.0" encoding="UTF-8"?>
            <AnalysisInput>
              <Rules>
                <Rule>
                  <Key>S107</Key>
                  <Parameters>
                    <Parameter>
                      <Key>max</Key>
                      <Value>3</Value>
                    </Parameter>
                  </Parameters>
                </Rule>
              </Rules>
            </AnalysisInput>
            """;

        var result = await new ProjectBuilder(fixture)
            .WithProperty("DotNetCodeQualitySonarLintXml", "SonarLint.xml")
            .WithSource("SonarLint.xml", sonarLint)
            .WithSource("Program.cs", MethodWithParameters(4))
            .BuildAsync();

        Assert.True(result.HasError("S107"), result.Output);
    }

    private static string MethodWithStatements(int count)
    {
        var statements = string.Join("\n", Enumerable.Range(1, count).Select(i => $"        System.Console.WriteLine({i});"));
        return $$"""
            namespace Sample;

            public static class Program
            {
                public static void Main()
                {
            {{statements}}
                }
            }
            """;
    }

    private static string MethodWithParameters(int count)
    {
        var parameters = string.Join(", ", Enumerable.Range(1, count).Select(i => $"int p{i}"));
        var sum = string.Join(" + ", Enumerable.Range(1, count).Select(i => $"p{i}"));
        var arguments = string.Join(", ", Enumerable.Range(1, count));
        return $$"""
            namespace Sample;

            public static class Program
            {
                public static void Main() => System.Console.WriteLine(Sum({{arguments}}));

                public static int Sum({{parameters}}) => {{sum}};
            }
            """;
    }

    private static string MethodWithSequentialIfs(int count)
    {
        var ifs = string.Join("\n", Enumerable.Range(1, count).Select(i => $$"""
                    if (value == {{i}})
                    {
                        System.Console.WriteLine({{i}});
                    }
            """));
        return $$"""
            namespace Sample;

            public static class Program
            {
                public static void Main() => Print(System.Environment.ProcessId);

                private static void Print(int value)
                {
            {{ifs}}
                }
            }
            """;
    }

    private static string ClassCoupledTo(int count)
    {
        var types = string.Join("\n", Enumerable.Range(1, count).Select(i => $"public sealed class Dependency{i};"));
        var properties = string.Join("\n", Enumerable.Range(1, count).Select(i => $"    public Dependency{i} Item{i} {{ get; }} = new();"));
        return $$"""
            namespace Sample;

            public static class Program
            {
                public static void Main() => System.Console.WriteLine(new Hub());
            }

            {{types}}

            public sealed class Hub
            {
            {{properties}}
            }
            """;
    }

    private static string InheritanceChain(int count)
    {
        var classes = string.Join("\n", Enumerable.Range(1, count).Select(i => i == 1 ? "public class Level1;" : $"public class Level{i} : Level{i - 1};"));
        return $$"""
            namespace Sample;

            public static class Program
            {
                public static void Main() => System.Console.WriteLine(new Level{{count}}());
            }

            {{classes}}
            """;
    }

    private static string FileWithLines(int count)
    {
        var methods = string.Join("\n", Enumerable.Range(1, count).Select(i => $"    public static void Method{i}() => System.Console.WriteLine({i});"));
        return $$"""
            namespace Sample;
            public static class Program
            {
                public static void Main() => Method1();
            {{methods}}
            }
            """;
    }

    private static string NestedIfs(int depth)
    {
        var lines = new List<string>();
        for (var i = 1; i <= depth; i++)
        {
            var indent = new string(' ', 8 + 4 * (i - 1));
            lines.Add($"{indent}System.Console.WriteLine({i});");
            lines.Add($"{indent}if (value > {i})");
            lines.Add($"{indent}{{");
        }

        lines.Add($"{new string(' ', 8 + 4 * depth)}System.Console.WriteLine(value);");
        for (var i = depth; i >= 1; i--)
        {
            lines.Add($"{new string(' ', 8 + 4 * (i - 1))}}}");
        }

        return $$"""
            namespace Sample;

            public static class Program
            {
                public static void Main() => Print(System.Environment.ProcessId);

                private static void Print(int value)
                {
            {{string.Join("\n", lines)}}
                }
            }
            """;
    }
}
