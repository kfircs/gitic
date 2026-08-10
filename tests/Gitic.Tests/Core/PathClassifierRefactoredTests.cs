using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Gitic.Tests
{
    public class PathClassifierRefactoredTests
    {
        private class MockFileSystem : IFileSystem
        {
            public Dictionary<string, byte[]> Files { get; } = new();

            public bool FileExists(string path) => Files.ContainsKey(path);

            public long GetFileSize(string path)
            {
                if (Files.TryGetValue(path, out var data))
                {
                    return data.Length;
                }
                throw new FileNotFoundException();
            }

            public Stream OpenRead(string path)
            {
                if (Files.TryGetValue(path, out var data))
                {
                    return new MemoryStream(data);
                }
                throw new FileNotFoundException();
            }
        }

        [Fact]
        public void TestLoadGitignoreRules_WithMockFileSystem()
        {
            var mockFs = new MockFileSystem();
            string repoRoot = "/mock/root";
            string gitignorePath = Path.Combine(repoRoot, ".gitignore");

            string gitignoreContent = "# This is a comment\r\nbin/\r\n*.log\r\n";
            mockFs.Files[gitignorePath] = Encoding.UTF8.GetBytes(gitignoreContent);

            var rules = PathClassifier.LoadGitignoreRules(repoRoot, mockFs);

            Assert.NotNull(rules);
            Assert.Equal(4, rules.Count);
            Assert.Contains(rules, r => r.Pattern == "bin/**");
            Assert.Contains(rules, r => r.Pattern == "**/*.log");
        }

        [Fact]
        public void TestLoadGitignoreRules_WithMissingGitignore()
        {
            var mockFs = new MockFileSystem();
            string repoRoot = "/mock/root";

            var rules = PathClassifier.LoadGitignoreRules(repoRoot, mockFs);

            Assert.NotNull(rules);
            Assert.Empty(rules);
        }
    }
}
