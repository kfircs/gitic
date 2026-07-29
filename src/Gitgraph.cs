using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public interface IGitGraph
    {
        HashSet<string> GetAncestors(
            string startCommitHash,
            Dictionary<string, GitCommitRecord> commitMap,
            int maxCount);

        List<GitCommitRecord> GetBranchCommits(
            string startCommitHash,
            HashSet<string> mainAncestors,
            Dictionary<string, GitCommitRecord> commitMap,
            int maxCount);

        // Deeper interface methods using IReadOnlyDictionary for improved flexibility and read-only safety
        HashSet<string> GetAncestors(
            string startCommitHash,
            IReadOnlyDictionary<string, GitCommitRecord> commitMap,
            int maxCount)
        {
            var ancestors = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(startCommitHash);
            int count = 0;

            while (queue.Count > 0 && count < maxCount)
            {
                string curr = queue.Dequeue();
                if (ancestors.Add(curr))
                {
                    if (commitMap.TryGetValue(curr, out var rec) && rec.Parents != null)
                    {
                        foreach (var parent in rec.Parents)
                        {
                            queue.Enqueue(parent);
                        }
                    }
                    count++;
                }
            }
            return ancestors;
        }

        List<GitCommitRecord> GetBranchCommits(
            string startCommitHash,
            HashSet<string> mainAncestors,
            IReadOnlyDictionary<string, GitCommitRecord> commitMap,
            int maxCount)
        {
            var branchCommits = new List<GitCommitRecord>();
            var queue = new Queue<string>();
            queue.Enqueue(startCommitHash);
            var visited = new HashSet<string>();
            int count = 0;

            while (queue.Count > 0 && count < maxCount)
            {
                string curr = queue.Dequeue();
                if (mainAncestors.Contains(curr))
                {
                    continue;
                }
                if (visited.Add(curr))
                {
                    if (commitMap.TryGetValue(curr, out var rec))
                    {
                        branchCommits.Add(rec);
                        if (rec.Parents != null)
                        {
                            foreach (var parent in rec.Parents)
                            {
                                queue.Enqueue(parent);
                            }
                        }
                    }
                    count++;
                }
            }
            return branchCommits;
        }

        List<GitCommitRecord> GetBranchCommitsForMerge(
            string p1,
            string p2,
            IReadOnlyDictionary<string, GitCommitRecord> commitMap,
            int mainAncestorsMaxDepth = 150,
            int branchCommitsMaxDepth = 100)
        {
            var mainAncestors = GetAncestors(p1, commitMap, mainAncestorsMaxDepth);
            return GetBranchCommits(p2, mainAncestors, commitMap, branchCommitsMaxDepth);
        }
    }

    public class GitGraphCalculator : IGitGraph
    {
        public HashSet<string> GetAncestors(
            string startCommitHash,
            Dictionary<string, GitCommitRecord> commitMap,
            int maxCount)
        {
            return new GitCommitGraph(commitMap.Values).GetAncestors(startCommitHash, maxCount);
        }

        public List<GitCommitRecord> GetBranchCommits(
            string startCommitHash,
            HashSet<string> mainAncestors,
            Dictionary<string, GitCommitRecord> commitMap,
            int maxCount)
        {
            return new GitCommitGraph(commitMap.Values).GetBranchCommits(startCommitHash, mainAncestors, maxCount);
        }
    }

    /// <summary>
    /// A deep domain model encapsulating the Git commit graph and supporting high-leverage structural queries.
    /// By packaging the underlying commit map, callers do not have to manage traversal structures or details.
    /// </summary>
    public class GitCommitGraph
    {
        private readonly Dictionary<string, GitCommitRecord> _commitMap;

        public GitCommitGraph(IEnumerable<GitCommitRecord> commits)
        {
            _commitMap = commits.ToDictionary(c => c.Hash, c => c, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyDictionary<string, GitCommitRecord> CommitMap => _commitMap;

        public bool ContainsCommit(string hash) => _commitMap.ContainsKey(hash);

        public bool TryGetCommit(string hash, out GitCommitRecord? record)
        {
            var found = _commitMap.TryGetValue(hash, out var rec);
            record = rec;
            return found;
        }

        public HashSet<string> GetAncestors(string startCommitHash, int maxCount)
        {
            var ancestors = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(startCommitHash);
            int count = 0;

            while (queue.Count > 0 && count < maxCount)
            {
                string curr = queue.Dequeue();
                if (ancestors.Add(curr))
                {
                    if (_commitMap.TryGetValue(curr, out var rec) && rec.Parents != null)
                    {
                        foreach (var parent in rec.Parents)
                        {
                            queue.Enqueue(parent);
                        }
                    }
                    count++;
                }
            }
            return ancestors;
        }

        public List<GitCommitRecord> GetBranchCommits(string startCommitHash, HashSet<string> mainAncestors, int maxCount)
        {
            var branchCommits = new List<GitCommitRecord>();
            var queue = new Queue<string>();
            queue.Enqueue(startCommitHash);
            var visited = new HashSet<string>();
            int count = 0;

            while (queue.Count > 0 && count < maxCount)
            {
                string curr = queue.Dequeue();
                if (mainAncestors.Contains(curr))
                {
                    continue;
                }
                if (visited.Add(curr))
                {
                    if (_commitMap.TryGetValue(curr, out var rec))
                    {
                        branchCommits.Add(rec);
                        if (rec.Parents != null)
                        {
                            foreach (var parent in rec.Parents)
                            {
                                queue.Enqueue(parent);
                            }
                        }
                    }
                    count++;
                }
            }
            return branchCommits;
        }

        public List<GitCommitRecord> GetBranchCommitsForMerge(
            string p1,
            string p2,
            int mainAncestorsMaxDepth = 150,
            int branchCommitsMaxDepth = 100)
        {
            var mainAncestors = GetAncestors(p1, mainAncestorsMaxDepth);
            return GetBranchCommits(p2, mainAncestors, branchCommitsMaxDepth);
        }
    }
}
