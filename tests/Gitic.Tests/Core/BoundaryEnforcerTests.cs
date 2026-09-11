using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Gitic.Tests;

/// <summary>
/// G1: boundary enforcer flags coupling pairs spanning forbidden source and target area globs.
/// CHECK: dotnet test --filter "FullyQualifiedName~BoundaryViolationDetectionTests"
/// </summary>
public class BoundaryViolationDetectionTests
{
    [Fact]
    public void EnforceBoundaries_FlagsCoupling_WhenSourceCouplesWithForbiddenArea()
    {
        var enforcer = new BoundaryEnforcer();
        var rules = new List<BoundaryRule>
        {
            new BoundaryRule
            {
                Name = "Core must not depend on Cli",
                Source = "src/Core/**",
                ForbiddenCoupling = "src/Cli/**",
                Threshold = 0.2
            }
        };

        var couplings = new List<TemporalCoupling>
        {
            new()
            {
                FileA = "src/Core/Analyzer.cs",
                FileB = "src/Cli/CliRoot.cs",
                CouplingDegree = 0.45,
                SharedCommits = 12
            }
        };

        var report = enforcer.EnforceBoundaries(couplings, rules);

        Assert.False(report.Passed);
        Assert.Single(report.Violations);
        var violation = report.Violations[0];
        Assert.Equal("Core must not depend on Cli", violation.RuleName);
        Assert.Equal("src/Core/Analyzer.cs", violation.FileA);
        Assert.Equal("src/Cli/CliRoot.cs", violation.FileB);
        Assert.Equal(0.45, violation.CouplingDegree);
        Assert.Equal(12, violation.SharedCommits);
        Assert.Equal(1, report.TotalRulesChecked);
        Assert.Equal(0, report.RulesPassed);
        Assert.Equal(1, report.RulesFailed);
    }

    [Fact]
    public void EnforceBoundaries_FlagsCoupling_WhenFileOrderIsReversedInCoupling()
    {
        var enforcer = new BoundaryEnforcer();
        var rules = new List<BoundaryRule>
        {
            new BoundaryRule
            {
                Name = "Core must not depend on Cli",
                Source = "src/Core/**",
                ForbiddenCoupling = "src/Cli/**",
                Threshold = 0.2
            }
        };

        // Here FileA is Cli and FileB is Core
        var couplings = new List<TemporalCoupling>
        {
            new()
            {
                FileA = "src/Cli/Commands/GateCommand.cs",
                FileB = "src/Core/Types.cs",
                CouplingDegree = 0.35,
                SharedCommits = 8
            }
        };

        var report = enforcer.EnforceBoundaries(couplings, rules);

        Assert.False(report.Passed);
        Assert.Single(report.Violations);
        var violation = report.Violations[0];
        Assert.Equal("Core must not depend on Cli", violation.RuleName);
        // Source should be FileA and target FileB
        Assert.Equal("src/Core/Types.cs", violation.FileA);
        Assert.Equal("src/Cli/Commands/GateCommand.cs", violation.FileB);
        Assert.Equal(0.35, violation.CouplingDegree);
        Assert.Equal(8, violation.SharedCommits);
    }

    [Fact]
    public void EnforceBoundaries_IgnoresCoupling_WhenCouplingDegreeBelowThreshold()
    {
        var enforcer = new BoundaryEnforcer();
        var rules = new List<BoundaryRule>
        {
            new BoundaryRule
            {
                Name = "Core must not depend on Cli",
                Source = "src/Core/**",
                ForbiddenCoupling = "src/Cli/**",
                Threshold = 0.30
            }
        };

        var couplings = new List<TemporalCoupling>
        {
            new()
            {
                FileA = "src/Core/Scoring.cs",
                FileB = "src/Cli/Program.cs",
                CouplingDegree = 0.15, // Below 0.30
                SharedCommits = 3
            }
        };

        var report = enforcer.EnforceBoundaries(couplings, rules);

        Assert.True(report.Passed);
        Assert.Empty(report.Violations);
        Assert.Equal(1, report.TotalRulesChecked);
        Assert.Equal(1, report.RulesPassed);
        Assert.Equal(0, report.RulesFailed);
    }

    [Fact]
    public void EnforceBoundaries_AllowsCoupling_WhenTargetMatchesAllowedCouplingGlob()
    {
        var enforcer = new BoundaryEnforcer();
        var rules = new List<BoundaryRule>
        {
            new BoundaryRule
            {
                Name = "Reporting reads Core, never writes Git",
                Source = "src/Reporting/**",
                AllowedCoupling = "src/Core/**",
                ForbiddenCoupling = "src/Git/**",
                Threshold = 0.20
            }
        };

        var couplings = new List<TemporalCoupling>
        {
            // Allowed coupling
            new()
            {
                FileA = "src/Reporting/JsonRenderer.cs",
                FileB = "src/Core/Types.cs",
                CouplingDegree = 0.65,
                SharedCommits = 20
            },
            // Forbidden coupling
            new()
            {
                FileA = "src/Reporting/SvgRenderer.cs",
                FileB = "src/Git/Gitparser.cs",
                CouplingDegree = 0.31,
                SharedCommits = 9
            }
        };

        var report = enforcer.EnforceBoundaries(couplings, rules);

        Assert.False(report.Passed);
        Assert.Single(report.Violations);
        Assert.Equal("src/Reporting/SvgRenderer.cs", report.Violations[0].FileA);
        Assert.Equal("src/Git/Gitparser.cs", report.Violations[0].FileB);
        Assert.Equal(0.31, report.Violations[0].CouplingDegree);
    }

    [Fact]
    public void EnforceBoundaries_AllowsInternalCoupling_WithinSameModule()
    {
        var enforcer = new BoundaryEnforcer();
        var rules = new List<BoundaryRule>
        {
            new BoundaryRule
            {
                Name = "Core must not depend on Cli",
                Source = "src/Core/**",
                ForbiddenCoupling = "src/Cli/**",
                Threshold = 0.20
            }
        };

        var couplings = new List<TemporalCoupling>
        {
            new()
            {
                FileA = "src/Core/Scoring.cs",
                FileB = "src/Core/AnalysisPipeline.cs",
                CouplingDegree = 0.78,
                SharedCommits = 32
            }
        };

        var report = enforcer.EnforceBoundaries(couplings, rules);

        Assert.True(report.Passed);
        Assert.Empty(report.Violations);
    }

    [Fact]
    public void EnforceBoundaries_HandlesMultipleForbiddenGlobs()
    {
        var enforcer = new BoundaryEnforcer();
        var rules = new List<BoundaryRule>
        {
            new BoundaryRule
            {
                Name = "Git module is self-contained",
                Source = "src/Git/**",
                ForbiddenCoupling = new GlobList { "src/Core/**", "src/Cli/**", "src/Reporting/**" },
                Threshold = 0.20
            }
        };

        var couplings = new List<TemporalCoupling>
        {
            new()
            {
                FileA = "src/Git/Gitgraph.cs",
                FileB = "src/Core/Analyzer.cs",
                CouplingDegree = 0.67,
                SharedCommits = 25
            },
            new()
            {
                FileA = "src/Git/Git.cs",
                FileB = "src/Cli/TuiNode.cs",
                CouplingDegree = 0.40,
                SharedCommits = 11
            },
            new()
            {
                FileA = "src/Git/Git.cs",
                FileB = "src/Git/Gitparser.cs",
                CouplingDegree = 0.80,
                SharedCommits = 40
            }
        };

        var report = enforcer.EnforceBoundaries(couplings, rules);

        Assert.False(report.Passed);
        Assert.Equal(2, report.Violations.Count);
        Assert.Contains(report.Violations, v => v.FileB == "src/Core/Analyzer.cs" && v.CouplingDegree == 0.67);
        Assert.Contains(report.Violations, v => v.FileB == "src/Cli/TuiNode.cs" && v.CouplingDegree == 0.40);
    }

    [Fact]
    public void EnforceBoundaries_SupportsMultipleRulesSimultaneously()
    {
        var enforcer = new BoundaryEnforcer();
        var rules = new List<BoundaryRule>
        {
            new BoundaryRule
            {
                Name = "Core must not depend on Cli",
                Source = "src/Core/**",
                ForbiddenCoupling = "src/Cli/**",
                Threshold = 0.20
            },
            new BoundaryRule
            {
                Name = "Git is self-contained",
                Source = "src/Git/**",
                ForbiddenCoupling = "src/Cli/**",
                Threshold = 0.20
            }
        };

        var couplings = new List<TemporalCoupling>
        {
            new() { FileA = "src/Core/A.cs", FileB = "src/Cli/B.cs", CouplingDegree = 0.5, SharedCommits = 10 },
            new() { FileA = "src/Git/C.cs", FileB = "src/Cli/D.cs", CouplingDegree = 0.4, SharedCommits = 8 },
            new() { FileA = "src/Core/A.cs", FileB = "src/Git/C.cs", CouplingDegree = 0.3, SharedCommits = 5 }
        };

        var report = enforcer.EnforceBoundaries(couplings, rules);

        Assert.False(report.Passed);
        Assert.Equal(2, report.TotalRulesChecked);
        Assert.Equal(0, report.RulesPassed);
        Assert.Equal(2, report.RulesFailed);
        Assert.Equal(2, report.Violations.Count);
    }

    [Fact]
    public void EnforceBoundaries_AllowsOverridingForbiddenGlobWithAllowedGlob()
    {
        var enforcer = new BoundaryEnforcer();
        var rules = new List<BoundaryRule>
        {
            new BoundaryRule
            {
                Name = "Allow Core override",
                Source = "src/Core/**",
                ForbiddenCoupling = "src/**",
                AllowedCoupling = "src/Core/**",
                Threshold = 0.20
            }
        };

        var couplings = new List<TemporalCoupling>
        {
            new() { FileA = "src/Core/A.cs", FileB = "src/Core/B.cs", CouplingDegree = 0.8 },
            new() { FileA = "src/Core/A.cs", FileB = "src/Cli/C.cs", CouplingDegree = 0.5 }
        };

        var report = enforcer.EnforceBoundaries(couplings, rules);

        Assert.False(report.Passed);
        Assert.Single(report.Violations);
        Assert.Equal("src/Cli/C.cs", report.Violations[0].FileB);
    }

    [Fact]
    public void EnforceBoundaries_HandlesNullOrEmptyInputsGracefully()
    {
        var enforcer = new BoundaryEnforcer();

        var emptyReport = enforcer.EnforceBoundaries((List<TemporalCoupling>?)null, (List<BoundaryRule>?)null);
        Assert.True(emptyReport.Passed);
        Assert.Empty(emptyReport.Violations);
        Assert.Equal(0, emptyReport.TotalRulesChecked);

        var emptyListReport = enforcer.EnforceBoundaries(new List<TemporalCoupling>(), new List<BoundaryRule>());
        Assert.True(emptyListReport.Passed);
        Assert.Empty(emptyListReport.Violations);

        var ruleList = new List<BoundaryRule>
        {
            new() { Name = "Test", Source = "src/Core/**", ForbiddenCoupling = "src/Cli/**" }
        };

        var nullCouplingReport = enforcer.EnforceBoundaries((List<TemporalCoupling>?)null, ruleList);
        Assert.True(nullCouplingReport.Passed);
        Assert.Empty(nullCouplingReport.Violations);
        Assert.Equal(1, nullCouplingReport.TotalRulesChecked);
        Assert.Equal(1, nullCouplingReport.RulesPassed);
    }

    [Fact]
    public void EnforceBoundaries_WithAnalysisResultAndGiticConfig_AttachesReportToResult()
    {
        var enforcer = new BoundaryEnforcer();
        var config = new GiticConfig
        {
            Boundaries = new List<BoundaryRule>
            {
                new()
                {
                    Name = "Core must not depend on Cli",
                    Source = "src/Core/**",
                    ForbiddenCoupling = "src/Cli/**",
                    Threshold = 0.2
                }
            }
        };

        var result = new AnalysisResult
        {
            TemporalCoupling = new List<TemporalCoupling>
            {
                new()
                {
                    FileA = "src/Core/Foo.cs",
                    FileB = "src/Cli/Bar.cs",
                    CouplingDegree = 0.55,
                    SharedCommits = 14
                }
            }
        };

        var report = enforcer.EnforceBoundaries(result, config);

        Assert.False(report.Passed);
        Assert.NotNull(result.BoundaryEnforcement);
        Assert.Same(report, result.BoundaryEnforcement);
        Assert.False(result.BoundaryEnforcement.Passed);
        Assert.Single(result.BoundaryEnforcement.Violations);
    }
}

/// <summary>
/// G2: boundary diagnostics output clear violation paths and coupling degrees.
/// CHECK: dotnet test --filter "FullyQualifiedName~BoundaryDiagnosticReportingTests"
/// </summary>
public class BoundaryDiagnosticReportingTests
{
    [Fact]
    public void BoundaryViolation_ContainsClearPathsAndCouplingDegreeInMessage()
    {
        var enforcer = new BoundaryEnforcer();
        var rules = new List<BoundaryRule>
        {
            new BoundaryRule
            {
                Name = "Core must not depend on Cli",
                Source = "src/Core/**",
                ForbiddenCoupling = "src/Cli/**",
                Threshold = 0.2
            }
        };

        var couplings = new List<TemporalCoupling>
        {
            new()
            {
                FileA = "src/Core/Types.cs",
                FileB = "src/Cli/Program.cs",
                CouplingDegree = 0.65,
                SharedCommits = 14
            }
        };

        var report = enforcer.EnforceBoundaries(couplings, rules);
        var violation = Assert.Single(report.Violations);

        Assert.Contains("src/Core/Types.cs", violation.ViolationMessage);
        Assert.Contains("src/Cli/Program.cs", violation.ViolationMessage);
        Assert.Contains("0.65", violation.ViolationMessage);
        Assert.Contains("14", violation.ViolationMessage);
        Assert.Contains("Core must not depend on Cli", violation.ViolationMessage);
        Assert.Equal(violation.ViolationMessage, violation.ToString());
    }

    [Fact]
    public void FormatSummary_OutputsPass_WhenNoViolationsDetected()
    {
        var enforcer = new BoundaryEnforcer();
        var rules = new List<BoundaryRule>
        {
            new BoundaryRule
            {
                Name = "Core must not depend on Cli",
                Source = "src/Core/**",
                ForbiddenCoupling = "src/Cli/**",
                Threshold = 0.2
            }
        };

        var report = enforcer.EnforceBoundaries(new List<TemporalCoupling>(), rules);
        string summary = enforcer.FormatSummary(report);

        Assert.Contains("Architectural Boundary Report", summary);
        Assert.Contains("PASS", summary);
        Assert.Contains("Core must not depend on Cli", summary);
        Assert.Contains("0 coupling pairs found crossing this boundary", summary);
    }

    [Fact]
    public void FormatSummary_OutputsClearFailDetails_WithViolationPathsAndDegrees()
    {
        var enforcer = new BoundaryEnforcer();
        var rules = new List<BoundaryRule>
        {
            new BoundaryRule
            {
                Name = "Reporting reads Core, never writes",
                Source = "src/Reporting/**",
                AllowedCoupling = "src/Core/**",
                ForbiddenCoupling = "src/Git/**",
                Threshold = 0.2
            }
        };

        var couplings = new List<TemporalCoupling>
        {
            new()
            {
                FileA = "src/Reporting/SvgRenderer.cs",
                FileB = "src/Git/Gitparser.cs",
                CouplingDegree = 0.31,
                SharedCommits = 7
            },
            new()
            {
                FileA = "src/Reporting/CliTableRenderer.cs",
                FileB = "src/Git/Git.cs",
                CouplingDegree = 0.22,
                SharedCommits = 5
            }
        };

        var report = enforcer.EnforceBoundaries(couplings, rules);
        string summary = enforcer.FormatSummary(report);

        Assert.Contains("Architectural Boundary Report", summary);
        Assert.Contains("FAIL", summary);
        Assert.Contains("Reporting reads Core, never writes", summary);
        Assert.Contains("src/Reporting/SvgRenderer.cs ↔ src/Git/Gitparser.cs", summary);
        Assert.Contains("0.31", summary);
        Assert.Contains("src/Reporting/CliTableRenderer.cs ↔ src/Git/Git.cs", summary);
        Assert.Contains("0.22", summary);
        Assert.Contains("Worst offender: src/Reporting/SvgRenderer.cs ↔ src/Git/Gitparser.cs (degree: 0.31)", summary);
    }

    [Fact]
    public void FormatSummary_HighlightsWorstOffender_WhenMultipleViolationsExist()
    {
        var enforcer = new BoundaryEnforcer();
        var rules = new List<BoundaryRule>
        {
            new BoundaryRule
            {
                Name = "Git module is self-contained",
                Source = "src/Git/**",
                ForbiddenCoupling = new GlobList { "src/Core/**", "src/Cli/**" },
                Threshold = 0.2
            }
        };

        var couplings = new List<TemporalCoupling>
        {
            new() { FileA = "src/Git/A.cs", FileB = "src/Core/A.cs", CouplingDegree = 0.25, SharedCommits = 4 },
            new() { FileA = "src/Git/Gitgraph.cs", FileB = "src/Core/Analyzer.cs", CouplingDegree = 0.67, SharedCommits = 22 },
            new() { FileA = "src/Git/B.cs", FileB = "src/Cli/B.cs", CouplingDegree = 0.45, SharedCommits = 10 }
        };

        var report = enforcer.EnforceBoundaries(couplings, rules);
        string summary = report.FormatSummary();

        Assert.Contains("Worst offender: src/Git/Gitgraph.cs ↔ src/Core/Analyzer.cs (degree: 0.67)", summary);
    }

    [Fact]
    public void FormatSummary_HandlesMixedPassAndFailRules()
    {
        var enforcer = new BoundaryEnforcer();
        var rules = new List<BoundaryRule>
        {
            new BoundaryRule
            {
                Name = "Core must not depend on Cli",
                Source = "src/Core/**",
                ForbiddenCoupling = "src/Cli/**",
                Threshold = 0.2
            },
            new BoundaryRule
            {
                Name = "Git module is self-contained",
                Source = "src/Git/**",
                ForbiddenCoupling = "src/Core/**",
                Threshold = 0.2
            }
        };

        var couplings = new List<TemporalCoupling>
        {
            new() { FileA = "src/Git/Gitgraph.cs", FileB = "src/Core/Analyzer.cs", CouplingDegree = 0.50, SharedCommits = 10 }
        };

        var report = enforcer.EnforceBoundaries(couplings, rules);
        string summary = enforcer.FormatSummary(report);

        Assert.Contains("PASS", summary);
        Assert.Contains("Core must not depend on Cli", summary);
        Assert.Contains("FAIL", summary);
        Assert.Contains("Git module is self-contained", summary);
        Assert.Contains("src/Git/Gitgraph.cs ↔ src/Core/Analyzer.cs", summary);
    }

    [Fact]
    public void BoundaryEnforcementReport_FormatSummaryMethod_MatchesEnforcerFormatSummary()
    {
        var enforcer = new BoundaryEnforcer();
        var rules = new List<BoundaryRule>
        {
            new BoundaryRule
            {
                Name = "Rule 1",
                Source = "src/Core/**",
                ForbiddenCoupling = "src/Cli/**"
            }
        };
        var couplings = new List<TemporalCoupling>
        {
            new() { FileA = "src/Core/A.cs", FileB = "src/Cli/B.cs", CouplingDegree = 0.4, SharedCommits = 5 }
        };

        var report = enforcer.EnforceBoundaries(couplings, rules);

        string reportSummary = report.FormatSummary();
        string enforcerSummary = enforcer.FormatSummary(report);

        Assert.Equal(reportSummary, enforcerSummary);
    }
}

/// <summary>
/// G3: configuration parser loads boundary rules from .gitic.yml without errors.
/// CHECK: dotnet test --filter "FullyQualifiedName~BoundaryConfigParsingTests"
/// </summary>
public class BoundaryConfigParsingTests
{
    [Fact]
    public void YamlParser_ParsesBoundaryRules_WithSingleForbiddenGlobString()
    {
        string yaml = @"
boundaries:
  - name: 'Core must not depend on Cli'
    source: 'src/Core/**'
    forbidden_coupling: 'src/Cli/**'
    threshold: 0.25
";

        var rules = BoundaryConfig.LoadRulesFromYaml(yaml, ".gitic.yml");

        Assert.Single(rules);
        var rule = rules[0];
        Assert.Equal("Core must not depend on Cli", rule.Name);
        Assert.Equal("src/Core/**", rule.Source);
        Assert.Single(rule.ForbiddenCoupling);
        Assert.Equal("src/Cli/**", rule.ForbiddenCoupling[0]);
        Assert.Equal(0.25, rule.Threshold);
        Assert.Empty(rule.AllowedCoupling);
    }

    [Fact]
    public void YamlParser_ParsesBoundaryRules_WithInlineForbiddenGlobArray()
    {
        string yaml = @"
boundaries:
  - name: 'Git module is self-contained'
    source: 'src/Git/**'
    forbidden_coupling: ['src/Core/**', 'src/Cli/**', 'src/Reporting/**']
";

        var rules = BoundaryConfig.LoadRulesFromYaml(yaml, ".gitic.yml");

        Assert.Single(rules);
        var rule = rules[0];
        Assert.Equal("Git module is self-contained", rule.Name);
        Assert.Equal("src/Git/**", rule.Source);
        Assert.Equal(3, rule.ForbiddenCoupling.Count);
        Assert.Equal("src/Core/**", rule.ForbiddenCoupling[0]);
        Assert.Equal("src/Cli/**", rule.ForbiddenCoupling[1]);
        Assert.Equal("src/Reporting/**", rule.ForbiddenCoupling[2]);
        Assert.Equal(0.2, rule.Threshold); // Default threshold
    }

    [Fact]
    public void YamlParser_ParsesBoundaryRules_WithMultilineForbiddenGlobSequence()
    {
        string yaml = @"
boundaries:
  - name: 'Git module is self-contained'
    source: 'src/Git/**'
    forbidden_coupling:
      - 'src/Core/**'
      - 'src/Cli/**'
      - 'src/Reporting/**'
";

        var rules = BoundaryConfig.LoadRulesFromYaml(yaml, ".gitic.yml");

        Assert.Single(rules);
        var rule = rules[0];
        Assert.Equal(3, rule.ForbiddenCoupling.Count);
        Assert.Equal("src/Core/**", rule.ForbiddenCoupling[0]);
        Assert.Equal("src/Cli/**", rule.ForbiddenCoupling[1]);
        Assert.Equal("src/Reporting/**", rule.ForbiddenCoupling[2]);
    }

    [Fact]
    public void YamlParser_ParsesAllowedCouplingAndCustomThreshold()
    {
        string yaml = @"
boundaries:
  - name: 'Reporting reads Core, never writes'
    source: 'src/Reporting/**'
    allowed_coupling: 'src/Core/**'
    forbidden_coupling: 'src/Git/**'
    threshold: 0.35
";

        var rules = BoundaryConfig.LoadRulesFromYaml(yaml, ".gitic.yml");

        Assert.Single(rules);
        var rule = rules[0];
        Assert.Equal("Reporting reads Core, never writes", rule.Name);
        Assert.Equal("src/Reporting/**", rule.Source);
        Assert.Single(rule.AllowedCoupling);
        Assert.Equal("src/Core/**", rule.AllowedCoupling[0]);
        Assert.Single(rule.ForbiddenCoupling);
        Assert.Equal("src/Git/**", rule.ForbiddenCoupling[0]);
        Assert.Equal(0.35, rule.Threshold);
    }

    [Fact]
    public void FullConfigOverrides_NormalizesAndMergesBoundariesSuccessfully()
    {
        string yaml = @"
aliases: []
areas: []
boundaries:
  - name: 'Core must not depend on Cli'
    source: 'src/Core/**'
    forbidden_coupling: 'src/Cli/**'
    threshold: 0.25
  - name: 'Git is self-contained'
    source: 'src/Git/**'
    forbidden_coupling: ['src/Core/**', 'src/Cli/**']
";

        var parsed = YamlSubsetParserHelper.ParseYamlSubset(yaml, ".gitic.yml");
        var validator = new ConfigValidator();
        validator.ValidateOverride(parsed, ".gitic.yml");

        var normalizer = new ConfigOverridesNormalizer(validator);
        var overrides = normalizer.NormalizeOverride(parsed, ".gitic.yml");

        Assert.NotNull(overrides.Boundaries);
        Assert.Equal(2, overrides.Boundaries.Count);

        var merger = new ConfigMerger();
        var merged = merger.MergeConfig(GiticConfig.Default, overrides);

        Assert.Equal(2, merged.Boundaries.Count);
        Assert.Equal("Core must not depend on Cli", merged.Boundaries[0].Name);
        Assert.Equal(0.25, merged.Boundaries[0].Threshold);
        Assert.Equal("Git is self-contained", merged.Boundaries[1].Name);
        Assert.Equal(2, merged.Boundaries[1].ForbiddenCoupling.Count);
    }

    [Fact]
    public void BoundaryValidator_ThrowsConfigValidationError_WhenBoundariesIsNotArray()
    {
        string yaml = @"
boundaries: 'invalid_string'
";
        var parsed = YamlSubsetParserHelper.ParseYamlSubset(yaml, ".gitic.yml");
        var validator = new ConfigValidator();

        var ex = Assert.Throws<ConfigValidationError>(() => validator.ValidateOverride(parsed, ".gitic.yml"));
        Assert.Contains("boundaries must be an array", string.Join(" ", ex.Details));
    }

    [Fact]
    public void BoundaryValidator_ThrowsConfigValidationError_WhenRuleHasMissingNameOrSource()
    {
        string yaml = @"
boundaries:
  - forbidden_coupling: 'src/Cli/**'
";
        var parsed = YamlSubsetParserHelper.ParseYamlSubset(yaml, ".gitic.yml");
        var validator = new ConfigValidator();

        var ex = Assert.Throws<ConfigValidationError>(() => validator.ValidateOverride(parsed, ".gitic.yml"));
        Assert.Contains("must be a non-empty string", string.Join(" ", ex.Details));
    }

    [Fact]
    public void BoundaryValidator_ThrowsConfigValidationError_WhenRuleHasUnknownKey()
    {
        string yaml = @"
boundaries:
  - name: 'Rule'
    source: 'src/Core/**'
    forbidden_coupling: 'src/Cli/**'
    unknown_property: 123
";
        var parsed = YamlSubsetParserHelper.ParseYamlSubset(yaml, ".gitic.yml");
        var validator = new ConfigValidator();

        var ex = Assert.Throws<ConfigValidationError>(() => validator.ValidateOverride(parsed, ".gitic.yml"));
        Assert.Contains("has unknown key \"unknown_property\"", string.Join(" ", ex.Details));
    }

    [Fact]
    public void BoundaryValidator_ThrowsConfigValidationError_WhenThresholdIsInvalid()
    {
        string yaml = @"
boundaries:
  - name: 'Rule'
    source: 'src/Core/**'
    forbidden_coupling: 'src/Cli/**'
    threshold: 2.5
";
        var parsed = YamlSubsetParserHelper.ParseYamlSubset(yaml, ".gitic.yml");
        var validator = new ConfigValidator();

        var ex = Assert.Throws<ConfigValidationError>(() => validator.ValidateOverride(parsed, ".gitic.yml"));
        Assert.Contains("threshold must be a number between 0 and 1", string.Join(" ", ex.Details));
    }

    [Fact]
    public void BoundaryConfig_ParseRules_SupportsDirectListOfRules()
    {
        string yaml = @"
- name: 'Root list rule'
  source: 'src/Core/**'
  forbidden_coupling: 'src/Cli/**'
";

        var rules = BoundaryConfig.ParseRules(yaml);

        Assert.Single(rules);
        Assert.Equal("Root list rule", rules[0].Name);
        Assert.Equal("src/Core/**", rules[0].Source);
        Assert.Equal("src/Cli/**", rules[0].ForbiddenCoupling[0]);
    }
}
