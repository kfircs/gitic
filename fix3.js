const fs = require('fs');
let tests = fs.readFileSync('tests/Gitic.Tests/PortedModulesTests.cs', 'utf8');

tests = tests.replace(/            engine\.TrackCommitFiles\(new List<string> \{ "fileA\.ts", "fileB\.ts" \}\);\r?\n            engine\.TrackCommitFiles\(new List<string> \{ "fileA\.ts", "fileB\.ts" \}\);\r?\n            engine\.TrackCommitFiles\(new List<string> \{ "fileA\.ts", "fileB\.ts" \}\);\r?\n\r?\n            var couplings = engine\.CalculateTemporalCoupling\(\);/g,
\`            var commits = new List<List<string>> {
                new List<string> { "fileA.ts", "fileB.ts" },
                new List<string> { "fileA.ts", "fileB.ts" },
                new List<string> { "fileA.ts", "fileB.ts" }
            };
            var couplings = engine.CalculateTemporalCoupling(commits);\`);

tests = tests.replace(/            engine\.TrackCommitFiles\(new List<string> \{ "fileA\.ts", "fileB\.ts" \}\);\r?\n            engine\.TrackCommitFiles\(new List<string> \{ "fileA\.ts", "fileB\.ts" \}\);\r?\n\r?\n            var couplings = engine\.CalculateTemporalCoupling\(\);/g,
\`            var commits2 = new List<List<string>> {
                new List<string> { "fileA.ts", "fileB.ts" },
                new List<string> { "fileA.ts", "fileB.ts" }
            };
            var couplings = engine.CalculateTemporalCoupling(commits2);\`);

tests = tests.replace(/            engine\.TrackCommitFiles\(new List<string> \{ "fileA\.ts", "fileB\.ts", "fileC\.ts" \}\);/g,
\`            engine.CalculateTemporalCoupling(new List<List<string>> { new List<string> { "fileA.ts", "fileB.ts", "fileC.ts" } });\`);

tests = tests.replace(/            engine\.TrackCommitFiles\(new List<string> \{ "file1\.ts", "file2\.ts" \}\);/g,
\`            engine.CalculateTemporalCoupling(new List<List<string>> { new List<string> { "file1.ts", "file2.ts" } });\`);

tests = tests.replace(/            normalEngine\.TrackCommitFiles\(new List<string> \{ "file1\.ts", "file2\.ts" \}\);/g,
\`            normalEngine.CalculateTemporalCoupling(new List<List<string>> { new List<string> { "file1.ts", "file2.ts" } });\`);

fs.writeFileSync('tests/Gitic.Tests/PortedModulesTests.cs', tests);
