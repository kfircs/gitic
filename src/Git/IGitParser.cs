using System.Collections.Generic;

namespace Gitic;

/// <summary>
/// Represents a deep interface for the main Git log parsing module.
/// Simplifies the surface area by only exposing high-leverage operations 
/// (building log arguments and parsing the complete log).
/// </summary>
public interface IGitParser
{
    string CommitMarker { get; }
    string NumstatMarker { get; }
    List<GitCommitRecord> ParseGitLog(string output);
    List<string> BuildGitLogArguments(GitHistoryExtractorOptions options);
}
