using System;
using System.Collections.Generic;
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

        public Task<string> RunAsync(string[] args, string cwd, CancellationToken cancellationToken = default)
        {
            _calls.Add(args);
            var key = GetKey(args);
            if (_outputs.TryGetValue(key, out string? value))
            {
                return Task.FromResult(value);
            }
            throw new Exception($"Unexpected Git command in MockGitExecutor: git {key}");
        }

        private static string GetKey(string[] args)
        {
            return string.Join(" | ", args);
        }
    }
}
