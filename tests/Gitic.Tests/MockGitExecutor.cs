using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic
{
    public class MockGitExecutor : IGitExecutor
    {
        private readonly Dictionary<string, string> _outputs = new(StringComparer.Ordinal);
        private readonly List<string[]> _calls = new();

        public IReadOnlyList<string[]> Calls => _calls;

        public void Setup(string[] args, string output)
        {
            var key = GetKey(args);
            _outputs[key] = output;
        }

        public async IAsyncEnumerable<string> RunAsync(string[] args, string cwd, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _calls.Add(args);
            var key = GetKey(args);
            if (_outputs.TryGetValue(key, out string? value))
            {
                using var reader = new StringReader(value);
                while (true)
                {
                    string? line = await reader.ReadLineAsync(cancellationToken);
                    if (line == null)
                    {
                        break;
                    }
                    yield return line;
                }
                yield break;
            }
            throw new Exception($"Unexpected Git command in MockGitExecutor: git {key}");
        }

        private static string GetKey(string[] args)
        {
            return string.Join(" | ", args);
        }
    }
}
