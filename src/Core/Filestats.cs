using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace Gitic;

public interface IFileStatsProvider
{
    Task<Dictionary<string, FileStatResult>> ComputeFileStatsAsync(
        string repoRoot,
        List<string> files,
        int concurrency = 20);

    Task<List<FileMetric>> EnrichFileMetricsAsync(
        string repoRoot,
        List<FileMetric> metrics,
        int concurrency = 20);
}

public class DiskFileStatsProvider : IFileStatsProvider
{
    private readonly IFileSystem _fileSystem;

    public DiskFileStatsProvider(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new PhysicalFileSystem();
    }

    public async Task<Dictionary<string, FileStatResult>> ComputeFileStatsAsync(
        string repoRoot,
        List<string> files,
        int concurrency = 20)
    {
        ConcurrentDictionary<string, FileStatResult> results = new();
        using SemaphoreSlim semaphore = new(concurrency);
        List<Task> tasks = new();

        foreach (var file in files)
        {
            await semaphore.WaitAsync();
            string currentFile = file;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    string fullPath = Path.Combine(repoRoot, currentFile);
                    if (_fileSystem.FileExists(fullPath))
                    {
                        long size = _fileSystem.GetFileSize(fullPath);
                        var (linesCount, width) = await AnalyzeFileContentAsync(fullPath, size);
                        results[currentFile] = new() { Size = size, Width = width, Lines = linesCount };
                    }
                    else
                    {
                        results[currentFile] = new() { Size = 0, Width = 0, Lines = 0 };
                    }
                }
                catch
                {
                    results[currentFile] = new() { Size = 0, Width = 0, Lines = 0 };
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
        return new(results);
    }

    public async Task<List<FileMetric>> EnrichFileMetricsAsync(
        string repoRoot,
        List<FileMetric> metrics,
        int concurrency = 20)
    {
        var files = metrics.Select(m => m.Path).ToList();
        var fileStats = await ComputeFileStatsAsync(repoRoot, files, concurrency);
        foreach (var f in metrics)
        {
            if (fileStats.TryGetValue(f.Path, out var stats))
            {
                f.Size = stats.Size;
                f.Width = stats.Width;
                f.Lines = stats.Lines;
            }
            else
            {
                f.Size = 0;
                f.Width = 0;
                f.Lines = 0;
            }
        }
        return metrics;
    }

    private async Task<(int LinesCount, int Width)> AnalyzeFileContentAsync(string fullPath, long size)
    {
        if (size == 0)
        {
            return (1, 0);
        }

        using (var stream = _fileSystem.OpenRead(fullPath))
        {
            if (await IsBinaryFileAsync(stream, size))
            {
                return (0, 0);
            }

            bool endsWithNewline = await EndsWithNewlineAsync(stream, size);
            var (linesCount, width) = await AnalyzeTextStreamAsync(stream);

            if (endsWithNewline)
            {
                linesCount++;
            }

            return (linesCount, width);
        }
    }

    private async Task<bool> IsBinaryFileAsync(Stream stream, long size)
    {
        stream.Seek(0, SeekOrigin.Begin);
        byte[] headerBuffer = new byte[Math.Min(8000, (int)Math.Min(size, int.MaxValue))];
        int bytesRead = await stream.ReadAsync(headerBuffer, 0, headerBuffer.Length);
        byte[] actualHeader = new byte[bytesRead];
        Array.Copy(headerBuffer, actualHeader, bytesRead);
        return FileStats.IsBinaryFile(actualHeader);
    }

    private async Task<bool> EndsWithNewlineAsync(Stream stream, long size)
    {
        stream.Seek(size - 1, SeekOrigin.Begin);
        byte[] lastByteBuf = new byte[1];
        int read = await stream.ReadAsync(lastByteBuf, 0, 1);
        return read > 0 && lastByteBuf[0] == 10; // '\n'
    }

    private async Task<(int LinesCount, int Width)> AnalyzeTextStreamAsync(Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        int linesCount = 0;
        int width = 0;

        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true))
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                linesCount++;
                if (line.Length > width)
                {
                    width = line.Length;
                }
            }
        }

        return (linesCount, width);
    }
}

public static class FileStats
{
    public static bool IsBinaryFile(byte[] buffer)
    {
        int limit = Math.Min(buffer.Length, 8000);
        for (int i = 0; i < limit; i++)
        {
            if (buffer[i] == 0)
            {
                return true;
            }
        }
        return false;
    }

    public static Task<Dictionary<string, FileStatResult>> ComputeFileStatsAsync(
        string repoRoot,
        List<string> files,
        int concurrency = 20)
    {
        DiskFileStatsProvider provider = new();
        return provider.ComputeFileStatsAsync(repoRoot, files, concurrency);
    }
}

public class FileStatResult
{
    public long Size { get; set; }
    public int Width { get; set; }
    public int Lines { get; set; }
}
