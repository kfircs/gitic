using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Gitic
{
    public class ConfigValidationError : Exception
    {
        public List<string> Details { get; }

        public ConfigValidationError(List<string> details) : base(string.Join("\n", details))
        {
            Details = details;
        }

        public ConfigValidationError(string message) : base(message)
        {
            Details = new List<string> { message };
        }
    }

    public class YamlLine
    {
        public int Indent { get; set; }
        public string Text { get; set; } = string.Empty;
        public int LineNumber { get; set; }
    }

    public class YamlTokenStream
    {
        private readonly List<YamlLine> _lines;
        private int _index = 0;

        public string Source { get; }

        public YamlTokenStream(List<YamlLine> lines, string source)
        {
            _lines = lines ?? throw new ArgumentNullException(nameof(lines));
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public bool HasMore => _index < _lines.Count;

        public YamlLine? Current => HasMore ? _lines[_index] : null;

        public YamlLine? Peek() => Current;

        public YamlLine? Next()
        {
            var line = Current;
            if (line != null)
            {
                _index++;
            }
            return line;
        }

        public void Consume()
        {
            if (HasMore)
            {
                _index++;
            }
        }

        public bool IsAtEnd() => !HasMore;

        public int LineNumber()
        {
            if (Current != null)
            {
                return Current.LineNumber;
            }
            if (_lines.Count > 0)
            {
                return _lines[_lines.Count - 1].LineNumber;
            }
            return 1;
        }

        public ConfigValidationError Error(YamlLine line, string detail)
        {
            return new ConfigValidationError(new List<string> { $"{Source}:{line.LineNumber}: {detail}" });
        }
    }

    public interface IYamlTokenizer
    {
        List<YamlLine> Tokenize(string content);
    }

    public class YamlTokenizer : IYamlTokenizer
    {
        private readonly string _source;

        public YamlTokenizer(string source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// Converts the input YAML content into a list of YamlLine structures.
        /// Performs initial validation (e.g., checks for unsupported tabs in indentation).
        /// </summary>
        public List<YamlLine> Tokenize(string content)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            var lines = new List<YamlLine>();
            var rawLines = content.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < rawLines.Length; i++)
            {
                string rawLine = rawLines[i];
                if (rawLine.Contains('\t'))
                {
                    throw new ConfigValidationError(new List<string> 
                    { 
                        $"{_source}:{i + 1}: tabs are not supported in YAML indentation." 
                    });
                }

                string lineWithoutComment = StripYamlComment(rawLine).TrimEnd();
                if (string.IsNullOrWhiteSpace(lineWithoutComment))
                {
                    continue;
                }

                int indent = lineWithoutComment.Length - lineWithoutComment.TrimStart().Length;
                lines.Add(new YamlLine
                {
                    Indent = indent,
                    Text = lineWithoutComment.TrimStart(),
                    LineNumber = i + 1
                });
            }

            return lines;
        }

        /// <summary>
        /// Finds the start index of a comment symbol ('#'), ignoring any '#' inside quotes.
        /// </summary>
        private int FindCommentStartIndex(string line)
        {
            bool inSingleQuotes = false;
            bool inDoubleQuotes = false;

            for (int index = 0; index < line.Length; index++)
            {
                char c = line[index];
                char previousChar = index > 0 ? line[index - 1] : '\0';

                if (c == '\'' && !inDoubleQuotes)
                {
                    inSingleQuotes = !inSingleQuotes;
                    continue;
                }

                if (c == '"' && !inSingleQuotes && previousChar != '\\')
                {
                    inDoubleQuotes = !inDoubleQuotes;
                    continue;
                }

                if (c == '#' && !inSingleQuotes && !inDoubleQuotes)
                {
                    return index;
                }
            }

            return -1;
        }

        private string StripYamlComment(string line)
        {
            int commentStart = FindCommentStartIndex(line);
            if (commentStart == -1)
            {
                return line;
            }
            return line.Substring(0, commentStart);
        }
    }

    public class YamlSubsetParser
    {
        private readonly YamlTokenStream _stream;
        public string Source { get; }

        public YamlSubsetParser(string content, string source, IYamlTokenizer? tokenizer = null)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            var activeTokenizer = tokenizer ?? new YamlTokenizer(source);
            var lines = activeTokenizer.Tokenize(content ?? string.Empty);
            _stream = new YamlTokenStream(lines, source);
        }

        /// <summary>
        /// Parses the token stream and returns the resulting dictionary/list hierarchy.
        /// </summary>
        public object? Parse()
        {
            if (_stream.IsAtEnd())
            {
                return new Dictionary<string, object?>();
            }

            var firstLine = _stream.Peek();
            int initialIndent = firstLine != null ? firstLine.Indent : 0;
            return ParseNode(initialIndent);
        }

        /// <summary>
        /// Parses a node at the expected indentation level.
        /// </summary>
        private object? ParseNode(int indent)
        {
            var line = _stream.Peek();
            if (line == null || line.Indent < indent)
            {
                return new Dictionary<string, object?>();
            }

            VerifyIndentation(line, indent);

            if (line.Text.StartsWith("- "))
            {
                return ParseSequence(indent);
            }

            return ParseMapping(indent);
        }

        /// <summary>
        /// Parses a sequence block (YAML list) with items starting with "- ".
        /// </summary>
        private List<object?> ParseSequence(int indent)
        {
            var result = new List<object?>();

            while (!_stream.IsAtEnd())
            {
                var line = _stream.Peek();
                if (line == null || line.Indent < indent)
                {
                    break;
                }

                VerifyIndentation(line, indent);

                if (!line.Text.StartsWith("- "))
                {
                    break;
                }

                string remainder = line.Text.Substring(2).Trim();
                _stream.Consume();

                if (remainder.Length == 0)
                {
                    result.Add(ParseEmptySequenceItem(indent));
                    continue;
                }

                var splitResult = SplitMappingEntry(remainder);
                if (splitResult == null)
                {
                    result.Add(ParseScalar(remainder));
                    continue;
                }

                result.Add(ParseInlineMappingSequenceItem(remainder, indent, line.LineNumber));
            }

            return result;
        }

        /// <summary>
        /// Parses a mapping block (YAML dictionary) at the given indentation.
        /// </summary>
        private Dictionary<string, object?> ParseMapping(int indent)
        {
            var result = new Dictionary<string, object?>();

            while (!_stream.IsAtEnd())
            {
                var line = _stream.Peek();
                if (line == null || line.Indent < indent)
                {
                    break;
                }

                VerifyIndentation(line, indent);

                if (line.Text.StartsWith("- "))
                {
                    break;
                }

                _stream.Consume();
                ParseMappingEntryInto(result, line.Text, indent, line.LineNumber);
            }

            return result;
        }

        /// <summary>
        /// Parses an empty sequence item, either producing null or parsing a nested node if indented deeper.
        /// </summary>
        private object? ParseEmptySequenceItem(int indent)
        {
            var next = _stream.Peek();
            if (next == null || next.Indent <= indent)
            {
                return null;
            }
            return ParseNode(next.Indent);
        }

        /// <summary>
        /// Parses a sequence item containing an inline mapping (e.g. "- key: value") 
        /// and optionally multiple subsequent lines indented at itemIndent (indent + 2).
        /// </summary>
        private Dictionary<string, object?> ParseInlineMappingSequenceItem(string remainder, int indent, int lineNumber)
        {
            int itemIndent = indent + 2;
            var item = new Dictionary<string, object?>();
            ParseMappingEntryInto(item, remainder, itemIndent, lineNumber);

            while (!_stream.IsAtEnd())
            {
                var next = _stream.Peek();
                if (next == null || next.Indent < itemIndent)
                {
                    break;
                }

                if (next.Indent != itemIndent)
                {
                    throw _stream.Error(next, $"unexpected indentation level {next.Indent}; expected {itemIndent}.");
                }

                if (next.Text.StartsWith("- "))
                {
                    throw _stream.Error(next, "sequence item cannot contain a sibling list entry without a mapping key.");
                }

                _stream.Consume();
                ParseMappingEntryInto(item, next.Text, itemIndent, next.LineNumber);
            }

            return item;
        }

        /// <summary>
        /// Parses a single key-value entry from text and adds it to the destination dictionary.
        /// </summary>
        private void ParseMappingEntryInto(
            Dictionary<string, object?> destination,
            string text,
            int currentIndent,
            int lineNumber)
        {
            var splitResult = SplitMappingEntry(text);
            if (splitResult == null)
            {
                throw new ConfigValidationError(new List<string>
                {
                    $"{Source}:{lineNumber}: expected a key/value mapping entry, got \"{text}\"."
                });
            }

            string key = splitResult.Value.Key;
            string rawValue = splitResult.Value.Value;

            if (rawValue.Length > 0)
            {
                destination[key] = ParseScalar(rawValue);
                return;
            }

            var nextLine = _stream.Peek();
            if (nextLine == null || nextLine.Indent <= currentIndent)
            {
                destination[key] = null;
                return;
            }

            destination[key] = ParseNode(nextLine.Indent);
        }

        /// <summary>
        /// Splits a mapping line of format "key: value" into a KeyValuePair.
        /// Returns null if it is not a valid mapping entry.
        /// </summary>
        private KeyValuePair<string, string>? SplitMappingEntry(string text)
        {
            int colonIndex = text.IndexOf(':');
            if (colonIndex <= 0)
            {
                return null;
            }

            string key = text.Substring(0, colonIndex).Trim();
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            string value = text.Substring(colonIndex + 1).Trim();
            return new KeyValuePair<string, string>(key, value);
        }

        /// <summary>
        /// Parses a raw string value into its appropriate type (e.g. bool, double, long, list, dict, null, or string).
        /// </summary>
        private object? ParseScalar(string value)
        {
            switch (value)
            {
                case "[]": return new List<object?>();
                case "{}": return new Dictionary<string, object?>();
                case "null":
                case "~": return null;
                case "true": return true;
                case "false": return false;
            }

            if (Regex.IsMatch(value, @"^[-+]?\d+(\.\d+)?$"))
            {
                if (value.Contains('.'))
                {
                    if (double.TryParse(value, out double doubleValue))
                    {
                        return doubleValue;
                    }
                }
                else
                {
                    if (long.TryParse(value, out long longValue))
                    {
                        return longValue;
                    }
                }
            }

            if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                (value.StartsWith("'") && value.EndsWith("'")))
            {
                return Unquote(value);
            }

            return value;
        }

        /// <summary>
        /// Safely strips outer quotes from a string and processes escape sequences if double-quoted.
        /// </summary>
        private string Unquote(string value)
        {
            if (value.Length < 2)
            {
                return value;
            }

            char quote = value[0];
            string body = value.Substring(1, value.Length - 2);

            if (quote == '\'')
            {
                return body.Replace("''", "'");
            }

            return body
                .Replace("\\\"", "\"")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\\", "\\");
        }

        /// <summary>
        /// Verifies that the line's indentation level does not exceed the expected level.
        /// </summary>
        private void VerifyIndentation(YamlLine line, int expectedIndent)
        {
            if (line.Indent > expectedIndent)
            {
                throw _stream.Error(line, $"unexpected indentation level {line.Indent}; expected {expectedIndent}.");
            }
        }
    }

    public interface IYamlParser
    {
        object? Parse(string content, string source);
    }

    public class YamlSubsetParserImpl : IYamlParser
    {
        public object? Parse(string content, string source)
        {
            return new YamlSubsetParser(content, source).Parse();
        }
    }

    public static class YamlSubsetParserHelper
    {
        public static IYamlParser Instance { get; set; } = new YamlSubsetParserImpl();

        public static object? ParseYamlSubset(string content, string source)
        {
            return Instance.Parse(content, source);
        }
    }
}
