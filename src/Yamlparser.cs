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
            _lines = lines;
            Source = source;
        }

        public YamlLine? Peek()
        {
            if (_index < _lines.Count)
            {
                return _lines[_index];
            }
            return null;
        }

        public YamlLine? Next()
        {
            var line = Peek();
            if (line != null)
            {
                _index++;
            }
            return line;
        }

        public void Consume()
        {
            if (_index < _lines.Count)
            {
                _index++;
            }
        }

        public bool IsAtEnd()
        {
            return _index >= _lines.Count;
        }

        public int LineNumber()
        {
            var line = Peek();
            if (line != null)
            {
                return line.LineNumber;
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

    public class YamlTokenizer
    {
        private readonly string _source;

        public YamlTokenizer(string source)
        {
            _source = source;
        }

        public List<YamlLine> Tokenize(string content)
        {
            var lines = new List<YamlLine>();
            var rawLines = content.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < rawLines.Length; i++)
            {
                string rawLine = rawLines[i];
                if (rawLine.Contains('\t'))
                {
                    throw new ConfigValidationError(new List<string> { $"{_source}:{i + 1}: tabs are not supported in YAML indentation." });
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

        private int FindCommentStartIndex(string line)
        {
            bool inSingle = false;
            bool inDouble = false;

            for (int index = 0; index < line.Length; index++)
            {
                char c = line[index];
                char previous = index > 0 ? line[index - 1] : '\0';

                if (c == '\'' && !inDouble)
                {
                    inSingle = !inSingle;
                    continue;
                }

                if (c == '"' && !inSingle && previous != '\\')
                {
                    inDouble = !inDouble;
                    continue;
                }

                if (c == '#' && !inSingle && !inDouble)
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

        public YamlSubsetParser(string content, string source)
        {
            Source = source;
            var tokenizer = new YamlTokenizer(source);
            var lines = tokenizer.Tokenize(content);
            _stream = new YamlTokenStream(lines, source);
        }

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

        private object? ParseNode(int indent)
        {
            var line = _stream.Peek();
            if (line == null || line.Indent < indent)
            {
                return new Dictionary<string, object?>();
            }

            if (line.Indent > indent)
            {
                throw _stream.Error(line, $"unexpected indentation level {line.Indent}; expected {indent}.");
            }

            if (line.Text.StartsWith("- "))
            {
                return ParseSequence(indent);
            }

            return ParseMapping(indent);
        }

        private Dictionary<string, object?> ParseMapping(int indent)
        {
            var result = new Dictionary<string, object?>();

            while (!_stream.IsAtEnd())
            {
                var line = _stream.Peek();
                if (line == null)
                {
                    break;
                }

                if (line.Indent < indent)
                {
                    break;
                }

                if (line.Indent > indent)
                {
                    throw _stream.Error(line, $"unexpected indentation level {line.Indent}; expected {indent}.");
                }

                if (line.Text.StartsWith("- "))
                {
                    break;
                }

                _stream.Consume();
                ParseMappingEntryInto(result, line.Text, indent, line.LineNumber);
            }

            return result;
        }

        private List<object?> ParseSequence(int indent)
        {
            var result = new List<object?>();

            while (!_stream.IsAtEnd())
            {
                var line = _stream.Peek();
                if (line == null)
                {
                    break;
                }

                if (line.Indent < indent)
                {
                    break;
                }

                if (line.Indent > indent)
                {
                    throw _stream.Error(line, $"unexpected indentation level {line.Indent}; expected {indent}.");
                }

                if (!line.Text.StartsWith("- "))
                {
                    break;
                }

                string remainder = line.Text.Substring(2).Trim();
                _stream.Consume();

                if (remainder.Length == 0)
                {
                    var next = _stream.Peek();
                    if (next == null || next.Indent <= indent)
                    {
                        result.Add(null);
                    }
                    else
                    {
                        result.Add(ParseNode(next.Indent));
                    }
                    continue;
                }

                var mappingEntry = SplitMappingEntry(remainder);
                if (mappingEntry == null)
                {
                    result.Add(ParseScalar(remainder));
                    continue;
                }

                int itemIndent = indent + 2;
                var item = new Dictionary<string, object?>();
                ParseMappingEntryInto(item, remainder, itemIndent, line.LineNumber);

                while (!_stream.IsAtEnd())
                {
                    var next = _stream.Peek();
                    if (next == null)
                    {
                        break;
                    }

                    if (next.Indent < itemIndent)
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

                result.Add(item);
            }

            return result;
        }

        private void ParseMappingEntryInto(
            Dictionary<string, object?> target,
            string text,
            int currentIndent,
            int lineNumber)
        {
            var mappingEntry = SplitMappingEntry(text);
            if (mappingEntry == null)
            {
                throw new ConfigValidationError(new List<string> { $"{Source}:{lineNumber}: expected a key/value mapping entry, got \"{text}\"." });
            }

            string key = mappingEntry.Value.Key;
            string val = mappingEntry.Value.Value;

            if (val.Length > 0)
            {
                target[key] = ParseScalar(val);
                return;
            }

            var next = _stream.Peek();
            if (next == null || next.Indent <= currentIndent)
            {
                target[key] = null;
                return;
            }

            target[key] = ParseNode(next.Indent);
        }

        private KeyValuePair<string, string>? SplitMappingEntry(string text)
        {
            int separator = text.IndexOf(':');
            if (separator <= 0)
            {
                return null;
            }

            string key = text.Substring(0, separator).Trim();
            if (key.Length == 0)
            {
                return null;
            }

            string val = text.Substring(separator + 1).Trim();
            return new KeyValuePair<string, string>(key, val);
        }

        private object? ParseScalar(string value)
        {
            if (value == "[]")
            {
                return new List<object?>();
            }
            if (value == "{}")
            {
                return new Dictionary<string, object?>();
            }
            if (value == "null" || value == "~")
            {
                return null;
            }
            if (value == "true")
            {
                return true;
            }
            if (value == "false")
            {
                return false;
            }

            if (Regex.IsMatch(value, @"^[-+]?\d+(\.\d+)?$"))
            {
                if (value.Contains('.'))
                {
                    if (double.TryParse(value, out double dVal))
                    {
                        return dVal;
                    }
                }
                else
                {
                    if (long.TryParse(value, out long lVal))
                    {
                        return lVal;
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

        private string Unquote(string value)
        {
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
    }

    public static class YamlSubsetParserHelper
    {
        public static object? ParseYamlSubset(string content, string source)
        {
            return new YamlSubsetParser(content, source).Parse();
        }
    }
}
