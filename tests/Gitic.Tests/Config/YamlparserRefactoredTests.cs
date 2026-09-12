using System;
using System.Collections.Generic;
using Xunit;

namespace Gitic.Tests
{
    public class YamlparserRefactoredTests
    {
        [Fact]
        public void BasicMappingsAndScalars_ShouldParseCorrectly()
        {
            string yaml = @"
# Simple config test
identity:
  merge_on_email: true
metrics:
  temporal_coupling_max_commit_file_count: 50
  ratio: 0.75
  neg_int: -123
";

            var parsed = (Dictionary<string, object?>)YamlSubsetParserHelper.ParseYamlSubset(yaml, "test_source")!;

            Assert.NotNull(parsed);
            Assert.True(parsed.ContainsKey("identity"));

            var identity = (Dictionary<string, object?>)parsed["identity"]!;
            Assert.True((bool)identity["merge_on_email"]!);

            var metrics = (Dictionary<string, object?>)parsed["metrics"]!;
            Assert.Equal(50L, (long)metrics["temporal_coupling_max_commit_file_count"]!);
            Assert.Equal(0.75, (double)metrics["ratio"]!);
            Assert.Equal(-123L, (long)metrics["neg_int"]!);
        }

        [Fact]
        public void Sequences_ShouldParseCorrectly()
        {
            string yaml = @"
excludes:
  - pattern: 'node_modules/**'
    category: dependency
  - pattern: 'dist/**'
    category: build
";

            var parsed = (Dictionary<string, object?>)YamlSubsetParserHelper.ParseYamlSubset(yaml, "test_source")!;

            Assert.NotNull(parsed);

            var excludes = (List<object?>)parsed["excludes"]!;
            Assert.Equal(2, excludes.Count);

            var entry1 = (Dictionary<string, object?>)excludes[0]!;
            Assert.Equal("node_modules/**", (string)entry1["pattern"]!);
            Assert.Equal("dependency", (string)entry1["category"]!);

            var entry2 = (Dictionary<string, object?>)excludes[1]!;
            Assert.Equal("dist/**", (string)entry2["pattern"]!);
            Assert.Equal("build", (string)entry2["category"]!);
        }

        [Fact]
        public void TabsNotSupported_ShouldThrowConfigValidationError()
        {
            string yaml = "identity:\n\tmerge_on_email: true";

            Action act = () => YamlSubsetParserHelper.ParseYamlSubset(yaml, "test_source");

            Assert.Throws<ConfigValidationError>(act);
        }

        [Fact]
        public void CommentsWithQuotes_ShouldParseCorrectly()
        {
            string yaml = @"
key_with_hash: 'value # with hash' # actual comment
key_with_hash_double: ""value # with double hash"" # comment
";

            var parsed = (Dictionary<string, object?>)YamlSubsetParserHelper.ParseYamlSubset(yaml, "test_source")!;

            Assert.Equal("value # with hash", parsed["key_with_hash"]);
            Assert.Equal("value # with double hash", parsed["key_with_hash_double"]);
        }

        [Fact]
        public void EmptyAndNullValues_ShouldParseCorrectly()
        {
            string yaml = @"
empty_list: []
empty_dict: {}
null_value: null
tilde_null: ~
empty_implicit: 
";

            var parsed = (Dictionary<string, object?>)YamlSubsetParserHelper.ParseYamlSubset(yaml, "test_source")!;

            Assert.Empty((List<object?>)parsed["empty_list"]!);
            Assert.Empty((Dictionary<string, object?>)parsed["empty_dict"]!);
            Assert.Null(parsed["null_value"]);
            Assert.Null(parsed["tilde_null"]);
            Assert.Null(parsed["empty_implicit"]);
        }

        [Fact]
        public void UnquotingAndEscaping_ShouldParseCorrectly()
        {
            string yaml = "single_quoted: 'hello ''world'''\n" +
                          "double_quoted: \"hello \\\"world\\\"\\nnewline\\ttab\"";

            var parsed = (Dictionary<string, object?>)YamlSubsetParserHelper.ParseYamlSubset(yaml, "test_source")!;

            Assert.Equal("hello 'world'", parsed["single_quoted"]);
            Assert.Equal("hello \"world\"\nnewline\ttab", parsed["double_quoted"]);
        }

        [Fact]
        public void InvalidIndentation_ShouldThrowConfigValidationError()
        {
            string yaml = @"
key:
    nested: value
  sibling: bad_indent
";

            Action act = () => YamlSubsetParserHelper.ParseYamlSubset(yaml, "test_source");

            Assert.Throws<ConfigValidationError>(act);
        }

        [Fact]
        public void TokenStream_ShouldManageTokensCorrectly()
        {
            var lines = new List<YamlLine>
            {
                new YamlLine { Indent = 0, Text = "key: val", LineNumber = 1 }
            };
            var stream = new YamlTokenStream(lines, "test_source");

            bool initialHasMore = stream.HasMore;
            bool initialIsAtEnd = stream.IsAtEnd();
            int initialLineNumber = stream.LineNumber();
            var linePeek = stream.Peek();

            stream.Consume();

            bool afterConsumeHasMore = stream.HasMore;
            bool afterConsumeIsAtEnd = stream.IsAtEnd();

            Assert.True(initialHasMore);
            Assert.False(initialIsAtEnd);
            Assert.Equal(1, initialLineNumber);
            Assert.NotNull(linePeek);
            Assert.Equal("key: val", linePeek.Text);
            Assert.False(afterConsumeHasMore);
            Assert.True(afterConsumeIsAtEnd);
        }
    }
}
