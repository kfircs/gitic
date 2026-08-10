using System;
using Xunit;
using Gitic;

namespace Gitic.Tests
{
    public class ReportUtilsTests
    {
        [Theory]
        [InlineData("/Users/user/project/my-repo", "my-repo")]
        [InlineData("/Users/user/project/my-repo/", "my-repo")]
        [InlineData("my-repo", "my-repo")]
        [InlineData("", "Repository")]
        [InlineData(null, "Repository")]
        public void TestGetRepositoryName(string? repoRoot, string expected)
        {
            // Handle null specifically because GetRepositoryName expects string but let's test null handling if we want to be safe.
            // Wait, the parameter in GetRepositoryName is string, not string?. So we pass string.Empty or check null.
            string actual = ReportUtils.GetRepositoryName(repoRoot ?? string.Empty);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestFormatGeneratedAt_WithValidDate()
        {
            string input = "2023-10-27T15:30:00Z";
            // Parsed to a DateTimeOffset, since it is UTC "Z", parsedGenAt will be 2023-10-27 15:30:00 +00:00.
            // When ToString is called, depending on local culture/timezone representation, we should be careful.
            // However, ToString("yyyy-MM-dd HH:mm:ss") returns the component values.
            // Let's parse and check.
            var expectedParsed = DateTimeOffset.Parse(input);
            string expected = expectedParsed.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

            string actual = ReportUtils.FormatGeneratedAt(input);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestFormatGeneratedAt_WithInvalidDate()
        {
            string input = "not-a-date";
            string actual = ReportUtils.FormatGeneratedAt(input);
            Assert.Equal(input, actual);
        }
    }
}
