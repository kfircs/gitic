using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic;

public class MergeLeadTimeCalculator : IMergeLeadTimeCalculator
{
    private const double MsPerHour = 3600000.0;

    private readonly LeadTimeConfig _config;

    public MergeLeadTimeCalculator(LeadTimeConfig? config = null)
    {
        _config = config ?? new LeadTimeConfig();
    }

    public MergeLeadTimeRecord? CalculateMergeLeadTime(GitCommitRecord m, IGitCommitGraph commitGraph)
    {
        if (m.Parents == null || m.Parents.Count <= 1)
        {
            return null;
        }

        string p1 = m.Parents[0];
        string p2 = m.Parents[1];

        var mainAncestors = commitGraph.GetAncestors(p1, _config.MainAncestorsMaxDepth);
        var branchCommits = commitGraph.GetBranchCommits(p2, mainAncestors, _config.BranchCommitsMaxDepth);

        if (branchCommits.Count > 0)
        {
            var earliest = branchCommits.Aggregate(branchCommits[0], (oldest, curr) =>
                curr.Timestamp < oldest.Timestamp ? curr : oldest);

            double leadTimeMs = m.Timestamp - earliest.Timestamp;
            double leadTimeHours = ScoringUtils.RoundRatio(Math.Max(_config.MinHours, leadTimeMs / MsPerHour));

            var filesSet = branchCommits.SelectMany(bc => bc.Files).Select(f => f.Path).ToHashSet();

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
