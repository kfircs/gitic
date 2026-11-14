using System;
using System.Collections.Generic;
using System.Linq;
using static Gitic.TuiExplorer;

namespace Gitic;

public class CodeOwnersPerspective : ITuiPerspective
{
    private const int MaxOwnersShown = 8;
    private const int ShareColumnWidth = 7;
    private const int MinNameWidth = 8;

    public int PerspectiveId => 6;
    public string DisplayName => "Code Owners";

    public List<string> GetRightSidebarLines(TuiNode node, int width, AnalysisResult result)
    {
        var (scope, owners) = ResolveOwners(node, result);

        var lines = new List<string>
        {
            $"\x1b[1m{CatppuccinMocha.Pink}Code Owners{CatppuccinMocha.Reset}",
            $"{CatppuccinMocha.Gray}{scope}{CatppuccinMocha.Reset}",
            ""
        };

        if (owners.Count == 0)
        {
            lines.Add($"  {CatppuccinMocha.Text}No contributor record{CatppuccinMocha.Reset}");
            return lines;
        }

        int nameWidth = Math.Max(MinNameWidth, width - 4 * ShareColumnWidth - 4);
        lines.Add($"{CatppuccinMocha.Gray}  {"Owner".PadRight(nameWidth)}{Header("Commit")}{Header("+Lines")}{Header("-Lines")}{Header("Activ.")}{CatppuccinMocha.Reset}");

        foreach (var owner in owners.OrderByDescending(o => o.CommitShare).ThenBy(o => o.Name, StringComparer.Ordinal).Take(MaxOwnersShown))
        {
            string name = owner.Name.Length > nameWidth ? owner.Name[..(nameWidth - 1)] + "…" : owner.Name;
            lines.Add($"  {CatppuccinMocha.Text}{name.PadRight(nameWidth)}{CatppuccinMocha.Green}{Percent(owner.CommitShare)}{CatppuccinMocha.Yellow}{Percent(owner.AddedShare)}{CatppuccinMocha.Red}{Percent(owner.DeletedShare)}{CatppuccinMocha.Lavender}{Percent(owner.ActivityShare)}{CatppuccinMocha.Reset}");
        }

        if (owners.Count > MaxOwnersShown)
        {
            lines.Add($"  {CatppuccinMocha.Gray}+{owners.Count - MaxOwnersShown} more contributors{CatppuccinMocha.Reset}");
        }
        return lines;
    }

    private static string Header(string text) => text.PadLeft(ShareColumnWidth);

    private static string Percent(double share) => $"{share:P0}".PadLeft(ShareColumnWidth);

    private static (string Scope, List<ContributorShare> Owners) ResolveOwners(TuiNode node, AnalysisResult result)
    {
        if (!node.IsDirectory)
        {
            return ($"File: {node.RelativePath}", node.FileMetric?.Contributors ?? new List<ContributorShare>());
        }

        if (node.RelativePath == "")
        {
            return ("Repository-wide", result.Contributors.Select(ToShare).ToList());
        }

        var inside = result.Areas
            .Where(a => a.Area == node.RelativePath || a.Area.StartsWith(node.RelativePath + "/", StringComparison.Ordinal))
            .ToList();
        if (inside.Count > 0)
        {
            return inside.Count == 1
                ? ($"Area: {inside[0].Area}", inside[0].Contributors)
                : ($"Directory: {node.RelativePath} ({inside.Count} areas combined)", Combine(inside));
        }

        var enclosing = result.Areas
            .Where(a => node.RelativePath.StartsWith(a.Area + "/", StringComparison.Ordinal))
            .OrderByDescending(a => a.Area.Length)
            .FirstOrDefault();
        return enclosing != null
            ? ($"Area: {enclosing.Area} (enclosing {node.RelativePath})", enclosing.Contributors)
            : ($"Directory: {node.RelativePath}", new List<ContributorShare>());
    }

    private static ContributorShare ToShare(ContributorMetric c) => new()
    {
        Name = c.Name,
        Email = c.Email,
        Activity = c.TotalActivity,
        ActivityShare = c.ActivityShare,
        Commits = c.Commits,
        CommitShare = c.CommitShare,
        Added = c.Added,
        AddedShare = c.AddedShare,
        Deleted = c.Deleted,
        DeletedShare = c.DeletedShare
    };

    // A commit touching several of the combined areas is counted once per area, so combined commit shares are approximate.
    private static List<ContributorShare> Combine(List<AreaMetric> areas)
    {
        var totals = areas
            .SelectMany(a => a.Contributors)
            .GroupBy(c => IdentityUtils.IdentityKey(c.Name, c.Email))
            .Select(g => new ContributorShare
            {
                Name = g.First().Name,
                Email = g.First().Email,
                Activity = g.Sum(c => c.Activity),
                Commits = g.Sum(c => c.Commits),
                Added = g.Sum(c => c.Added),
                Deleted = g.Sum(c => c.Deleted)
            })
            .ToList();

        double activity = totals.Sum(c => c.Activity);
        double commits = totals.Sum(c => c.Commits);
        double added = totals.Sum(c => c.Added);
        double deleted = totals.Sum(c => c.Deleted);
        foreach (var c in totals)
        {
            c.ActivityShare = ScoringUtils.ShareOf(c.Activity, activity);
            c.CommitShare = ScoringUtils.ShareOf(c.Commits, commits);
            c.AddedShare = ScoringUtils.ShareOf(c.Added, added);
            c.DeletedShare = ScoringUtils.ShareOf(c.Deleted, deleted);
        }
        return totals;
    }
}
