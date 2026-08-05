using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic;

public class MergeLeadTimeCalculator : IMergeLeadTimeCalculator
{
    private const double MsPerHour = 3600000.0;

    private readonly IGitGraph _gitGraph;
    private readonly LeadTimeConfig _config;

    public MergeLeadTimeCalculator(IGitGraph? gitGraph = null)
    {
        _gitGraph = gitGraph ?? new GitGraphCalculator();
        _config = new LeadTimeConfig();
    }

    public MergeLeadTimeCalculator(IGitGraph? gitGraph, LeadTimeConfig? config)
    {
        _gitGraph = gitGraph ?? new GitGraphCalculator();
        _config = config ?? new LeadTimeConfig();
    }

    public MergeLeadTimeRecord? CalculateMergeLeadTime(GitCommitRecord m, Dictionary<string, GitCommitRecord> commitMap)
    {
        if (m.Parents == null || m.Parents.Count <= 1)
        {
            return null;
        }

        string p1 = m.Parents[0];
        string p2 = m.Parents[1];

        var mainAncestors = _gitGraph.GetAncestors(p1, commitMap, _config.MainAncestorsMaxDepth);
        var branchCommits = _gitGraph.GetBranchCommits(p2, mainAncestors, commitMap, _config.BranchCommitsMaxDepth);

        if (branchCommits.Count > 0)
        {
            var earliest = branchCommits.Aggregate(branchCommits[0], (oldest, curr) =>
                curr.Timestamp < oldest.Timestamp ? curr : oldest);

            double leadTimeMs = m.Timestamp - earliest.Timestamp;
            double leadTimeHours = ScoringUtils.RoundRatio(Math.Max(_config.MinHours, leadTimeMs / MsPerHour));

            var filesSet = new HashSet<string>();
            foreach (var bc in branchCommits)
            {
                foreach (var f in bc.Files)
                {
                    filesSet.Add(f.Path);
                }
            }

            return new MergeLeadTimeRecord
            {
                Hash = m.Hash,
                Message = m.Message.Split('\n')[0].Trim(),
                Author = m.Author.Name,
                Date = m.Date,
                LeadTimeHours = leadTimeHours,
                FileCount = filesSet.Count
            };
        }

        return null;
    }
}
