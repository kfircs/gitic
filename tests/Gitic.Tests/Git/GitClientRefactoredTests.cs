using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Gitic.Tests
{
    public class GitClientRefactoredTests
    {
        private class MockGitParser : IGitParser
        {
            public string CommitMarker => "MOCK_COMMIT";
            public string NumstatMarker => "MOCK_NUMSTAT";

            public List<string> BuildGitLogArguments(GitHistoryExtractorOptions options)
            {
                return new List<string> { "mock", "log" };
            }

            public List<GitCommitRecord> ParseGitLog(string output)
            {
                return new List<GitCommitRecord>
                {
                    new GitCommitRecord
                    {
                        Hash = "mock_hash",
                        Author = new GitIdentity { Name = "Mock Author", Email = "mock@example.com" },
                        Message = "Mock message",
                        Date = "2026-06-01T12:00:00Z",
                        Parents = new List<string>(),
                        CoAuthors = new List<GitIdentity>(),
                        Files = new List<GitFileChange>()
                    }
                };
            }
        }

        [Fact]
        public async Task TestGitClient_WithCustomParser_UsesMockParser()
        {
            var mockExecutor = new MockGitExecutor();
            mockExecutor.Setup(new[] { "mock", "log" }, "some dummy output");

            var mockParser = new MockGitParser();
            var client = new GitClient("/path/to/repo", mockExecutor, mockParser);

            var options = new GitHistoryExtractorOptions();
            var records = await client.ExtractHistoryAsync(options);

            Assert.Single(records);
            Assert.Equal("mock_hash", records[0].Hash);
            Assert.Equal("Mock message", records[0].Message);
        }

        [Fact]
        public void TestGitClient_WithNullParser_FallsBackToDefaultParser()
        {
            var client = new GitClient("/path/to/repo", null, null);
            Assert.NotNull(client);
        }
    }
}
