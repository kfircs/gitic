const fs = require('fs');

let tests = fs.readFileSync('tests/Gitic.Tests/PortedModulesTests.cs', 'utf8');

const t1 = `            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });
            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });
            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });

            var couplings = engine.CalculateTemporalCoupling();`;

const t1R = `            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });\r\n            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });\r\n            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });\r\n\r\n            var couplings = engine.CalculateTemporalCoupling();`;

const r1 = `            var couplings = engine.CalculateTemporalCoupling(new List<List<string>> {
                new List<string> { "fileA.ts", "fileB.ts" },
                new List<string> { "fileA.ts", "fileB.ts" },
                new List<string> { "fileA.ts", "fileB.ts" }
            });`;

tests = tests.replace(t1, r1).replace(t1R, r1);

const t2 = `            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });
            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });

            var couplings = engine.CalculateTemporalCoupling();`;

const t2R = `            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });\r\n            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts" });\r\n\r\n            var couplings = engine.CalculateTemporalCoupling();`;

const r2 = `            var couplings = engine.CalculateTemporalCoupling(new List<List<string>> {
                new List<string> { "fileA.ts", "fileB.ts" },
                new List<string> { "fileA.ts", "fileB.ts" }
            });`;

tests = tests.replace(t2, r2).replace(t2R, r2);

const t3 = `            engine.TrackCommitFiles(new List<string> { "fileA.ts", "fileB.ts", "fileC.ts" });`;
const r3 = `            engine.CalculateTemporalCoupling(new List<List<string>> { new List<string> { "fileA.ts", "fileB.ts", "fileC.ts" } });`;
tests = tests.split(t3).join(r3);

const t4 = `            engine.TrackCommitFiles(new List<string> { "file1.ts", "file2.ts" });`;
const r4 = `            engine.CalculateTemporalCoupling(new List<List<string>> { new List<string> { "file1.ts", "file2.ts" } });`;
tests = tests.split(t4).join(r4);

const t5 = `            normalEngine.TrackCommitFiles(new List<string> { "file1.ts", "file2.ts" });`;
const r5 = `            normalEngine.CalculateTemporalCoupling(new List<List<string>> { new List<string> { "file1.ts", "file2.ts" } });`;
tests = tests.split(t5).join(r5);

fs.writeFileSync('tests/Gitic.Tests/PortedModulesTests.cs', tests);
