using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic;

public interface IAreaMapper
{
    string AreaForPath(
        string path,
        int depth,
        List<NamedArea> namedAreas,
        HashSet<string>? warnings = null);
}

public class AreaMapper : IAreaMapper
{
    public string AreaForPath(
        string path,
        int depth,
        List<NamedArea> namedAreas,
        HashSet<string>? warnings = null)
    {
        var matchingAreas = namedAreas.Where(area =>
            area.Paths.Any(areaPath => PathUtils.MatchesPathPattern(path, areaPath))
        ).ToList();

        var firstMatch = matchingAreas.FirstOrDefault();
        if (firstMatch != null)
        {
            if (matchingAreas.Count > 1 && warnings != null)
            {
                warnings.Add(
                    $"Path {path} matched multiple configured areas ({string.Join(", ", matchingAreas.Select(a => a.Name))}); using {firstMatch.Name}."
                );
            }
            return firstMatch.Name;
        }

        var segments = PathUtils.NormalizeGitPath(path).Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 1)
        {
            return ".";
        }
        return string.Join("/", segments.Take(Math.Min(depth, segments.Length - 1)));
    }
}
