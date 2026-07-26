const fs = require('fs');

let tests = fs.readFileSync('tests/Gitic.Tests/PortedModulesTests.cs', 'utf8');

tests = tests.replace(/var calculator = new HeatScoreCalculator\(\);\r?\n/, '');
tests = tests.replace(/Assert\.Equal\(44, calculator\.Calculate\(breakdown\)\);/, 'Assert.Equal(44, ScoringUtils.CalculateHeatScore(breakdown));');

tests = tests.replace(/var calculator = new AttentionScoreCalculator\(weights\);\r?\n/, '');
tests = tests.replace(/Assert\.Equal\(50, calculator\.Calculate\(breakdown\)\);/, 'Assert.Equal(50, ScoringUtils.CalculateAttentionScore(breakdown, weights));');

fs.writeFileSync('tests/Gitic.Tests/PortedModulesTests.cs', tests);
