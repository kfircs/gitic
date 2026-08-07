using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gitic;

public class TuiNode
{
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public List<TuiNode> Children { get; set; } = new();

    // Aggregated LoC Stats
    public int FileCount { get; set; }
    public int TotalLines { get; set; }
    public int MinLines { get; set; }
    public int MaxLines { get; set; }
    
    // Aggregated Width Stats (Line Width in Characters)
    public int MinWidth { get; set; }
    public int MaxWidth { get; set; }
    public int TotalWidth { get; set; } // for calculating Average Width

    // Git / Hotspots aggregation
    public int TotalTouches { get; set; }
    public int TotalChurn { get; set; }
    public double MaxHeatScore { get; set; }
    public double MaxAttentionScore { get; set; }

    // Reference to raw FileMetric if IsDirectory is false
    public FileMetric? FileMetric { get; set; }

    // Aggregated Work Classification
    public WorkClassificationMetrics WorkClassification { get; set; } = new();

    public static bool IsExcluded(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return true;
        
        string normalized = relativePath.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
        {
            if (seg.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("nupkg", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals(".test-report", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("reports", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("logs", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        string ext = Path.GetExtension(normalized).ToLowerInvariant();
        if (Exclusions.NonCodeExtensions.Contains(ext) ||
            Exclusions.BinaryImageExtensions.Contains(ext) ||
            Exclusions.LockFiles.Contains(Path.GetFileName(normalized)))
        {
            return true;
        }

        if (ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public static TuiNode BuildTree(IEnumerable<FileMetric> files)
    {
        var root = new TuiNode
        {
            Name = "Root",
            RelativePath = "",
            IsDirectory = true
        };

        foreach (var file in files)
        {
            if (IsExcluded(file.Path))
            {
                continue;
            }

            string normalized = file.Path.Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(normalized)) continue;

            var segments = normalized.Split('/');
            var current = root;

            for (int i = 0; i < segments.Length; i++)
            {
                string segmentName = segments[i];
                bool isLast = (i == segments.Length - 1);
                string childRelativePath = string.Join("/", segments.Take(i + 1));

                var child = current.Children.FirstOrDefault(c => c.Name.Equals(segmentName, StringComparison.OrdinalIgnoreCase) && c.IsDirectory == !isLast);
                if (child == null)
                {
                    child = new TuiNode
                    {
                        Name = segmentName,
                        RelativePath = childRelativePath,
                        IsDirectory = !isLast
                    };
                    current.Children.Add(child);
                }

                if (isLast)
                {
                    child.FileMetric = file;
                }

                current = child;
            }
        }

        AggregateStats(root);
        SortTree(root);
        return root;
    }

    private static void SortTree(TuiNode node)
    {
        node.Children = node.Children
            .OrderByDescending(c => c.IsDirectory)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var child in node.Children)
        {
            SortTree(child);
        }
    }

    private static void AggregateStats(TuiNode node)
    {
        if (!node.IsDirectory)
        {
            node.FileCount = 1;
            node.TotalLines = node.FileMetric?.Lines ?? 0;
            node.MinLines = node.TotalLines;
            node.MaxLines = node.TotalLines;

            node.MinWidth = node.FileMetric?.Width ?? 0;
            node.MaxWidth = node.MinWidth;
            node.TotalWidth = node.MinWidth;

            node.TotalTouches = node.FileMetric?.Touches ?? 0;
            node.TotalChurn = node.FileMetric?.Churn ?? 0;
            node.MaxHeatScore = node.FileMetric?.HeatScore ?? 0.0;
            node.MaxAttentionScore = node.FileMetric?.AttentionScore ?? 0.0;
            if (node.FileMetric != null)
            {
                node.WorkClassification = node.FileMetric.WorkClassification ?? new();
            }
            return;
        }

        int fileCount = 0;
        int totalLines = 0;
        int maxLines = 0;
        int minLines = int.MaxValue;

        int maxWidth = 0;
        int minWidth = int.MaxValue;
        int totalWidth = 0;

        int totalTouches = 0;
        int totalChurn = 0;
        double maxHeatScore = 0.0;
        double maxAttentionScore = 0.0;

        var wc = new WorkClassificationMetrics();

        foreach (var child in node.Children)
        {
            AggregateStats(child);

            fileCount += child.FileCount;
            totalLines += child.TotalLines;
            totalWidth += child.TotalWidth;
            totalTouches += child.TotalTouches;
            totalChurn += child.TotalChurn;

            if (child.MaxHeatScore > maxHeatScore) maxHeatScore = child.MaxHeatScore;
            if (child.MaxAttentionScore > maxAttentionScore) maxAttentionScore = child.MaxAttentionScore;
            if (child.MaxLines > maxLines) maxLines = child.MaxLines;
            if (child.MaxWidth > maxWidth) maxWidth = child.MaxWidth;

            if (child.MinLines > 0 && child.MinLines < minLines)
            {
                minLines = child.MinLines;
            }
            if (child.MinWidth > 0 && child.MinWidth < minWidth)
            {
                minWidth = child.MinWidth;
            }

            wc.Features += child.WorkClassification.Features;
            wc.Bugs += child.WorkClassification.Bugs;
            wc.TechnicalDebt += child.WorkClassification.TechnicalDebt;
            wc.Chores += child.WorkClassification.Chores;
            wc.Unclassified += child.WorkClassification.Unclassified;
        }

        node.FileCount = fileCount;
        node.TotalLines = totalLines;
        node.TotalWidth = totalWidth;
        node.TotalTouches = totalTouches;
        node.TotalChurn = totalChurn;
        node.MaxHeatScore = maxHeatScore;
        node.MaxAttentionScore = maxAttentionScore;

        node.MaxLines = maxLines;
        node.MinLines = (minLines == int.MaxValue) ? 0 : minLines;

        node.MaxWidth = maxWidth;
        node.MinWidth = (minWidth == int.MaxValue) ? 0 : minWidth;

        node.WorkClassification = wc;
    }

    public TuiNode? FindMinLoCFile()
    {
        if (!IsDirectory) return this;
        TuiNode? best = null;
        int min = int.MaxValue;
        foreach (var child in Children)
        {
            var node = child.FindMinLoCFile();
            if (node != null && node.TotalLines < min && node.TotalLines > 0 && !node.IsDirectory)
            {
                min = node.TotalLines;
                best = node;
            }
        }
        return best;
    }

    public TuiNode? FindMaxLoCFile()
    {
        if (!IsDirectory) return this;
        TuiNode? best = null;
        int max = -1;
        foreach (var child in Children)
        {
            var node = child.FindMaxLoCFile();
            if (node != null && node.TotalLines > max && !node.IsDirectory)
            {
                max = node.TotalLines;
                best = node;
            }
        }
        return best;
    }

    public TuiNode? FindHighestAttentionFile()
    {
        if (!IsDirectory) return this;
        TuiNode? best = null;
        double max = -1.0;
        foreach (var child in Children)
        {
            var node = child.FindHighestAttentionFile();
            if (node != null && (node.FileMetric?.AttentionScore ?? 0.0) > max && !node.IsDirectory)
            {
                max = node.FileMetric?.AttentionScore ?? 0.0;
                best = node;
            }
        }
        return best;
    }
}
