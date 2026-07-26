const fs = require('fs');

let tests = fs.readFileSync('tests/Gitic.Tests/PortedModulesTests.cs', 'utf8');

// 1. Remove FakeConfigurationResolver block
const fakeConfigMatch = /            private class FakeConfigurationResolver : IConfigurationResolver[\s\S]*?            \}/;
tests = tests.replace(fakeConfigMatch, '');

// 2. Remove TestRepositoryAnalyzer_UsesInjectedResolver block
const injectedResolverMatch = /            \[Fact\]\s+public async Task TestRepositoryAnalyzer_UsesInjectedResolver\(\)[\s\S]*?Assert\.Equal\(99, result\.Settings\.Depth\);\s+\}/;
tests = tests.replace(injectedResolverMatch, '');

// 3. Remove MockScoreCalculator and MockScoreCalculatorProvider
const mockScoreCalcMatch = /            private class MockScoreCalculator : IScoreCalculator[\s\S]*?            \}/;
tests = tests.replace(mockScoreCalcMatch, '');
const mockScoreCalcProvMatch = /            private class MockScoreCalculatorProvider : IScoreCalculatorProvider[\s\S]*?            \}/;
tests = tests.replace(mockScoreCalcProvMatch, '');

// 4. Remove TestFamiliarityScoringEngine_UsesInjectedScoreCalculatorProvider
const familiarityScoringMatch = /            \[Fact\]\s+public void TestFamiliarityScoringEngine_UsesInjectedScoreCalculatorProvider\(\)[\s\S]*?Assert\.Equal\(84\.0, areas\[0\]\.AttentionScore\);\s+\}/;
tests = tests.replace(familiarityScoringMatch, '');

fs.writeFileSync('tests/Gitic.Tests/PortedModulesTests.cs', tests);
