using System;
using System.Collections.Generic;

namespace Gitic
{
    public static class GitGraph
    {
        public static HashSet<string> GetAncestors(
            string startCommitHash,
            Dictionary<string, GitCommitRecord> commitMap,
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
                    if (commitMap.TryGetValue(curr, out var rec))
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

        public static List<GitCommitRecord> GetBranchCommits(
            string startCommitHash,
            HashSet<string> mainAncestors,
            Dictionary<string, GitCommitRecord> commitMap,
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
                        foreach (var parent in rec.Parents)
                        {
                            queue.Enqueue(parent);
                        }
                    }
                    count++;
                }
            }
            return branchCommits;
        }
    }
}
