using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic
{
    public interface IFileStatsProvider
    {
        Task<Dictionary<string, FileStatResult>> ComputeFileStatsAsync(
            string repoRoot,
            List<string> files,
            int concurrency = 20);
    }

    public class DiskFileStatsProvider : IFileStatsProvider
    {
        public async Task<Dictionary<string, FileStatResult>> ComputeFileStatsAsync(
            string repoRoot,
            List<string> files,
            int concurrency = 20)
        {
            var results = new ConcurrentDictionary<string, FileStatResult>();
            using var semaphore = new SemaphoreSlim(concurrency);
            var tasks = new List<Task>();

            foreach (var file in files)
            {
                await semaphore.WaitAsync();
                string currentFile = file;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        string fullPath = Path.Combine(repoRoot, currentFile);
                        var fileInfo = new FileInfo(fullPath);
                        if (fileInfo.Exists)
                        {
                            long size = fileInfo.Length;
                            int width = 0;
                            int linesCount = 0;

                            if (size > 0)
                            {
                                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
                                {
                                    byte[] headerBuffer = new byte[Math.Min(8000, (int)Math.Min(size, int.MaxValue))];
                                    int bytesRead = await stream.ReadAsync(headerBuffer, 0, headerBuffer.Length);
                                    byte[] actualHeader = new byte[bytesRead];
                                    Array.Copy(headerBuffer, actualHeader, bytesRead);

                                    if (!FileStats.IsBinaryFile(actualHeader))
                                    {
                                        bool endsWithNewline = false;
                                        stream.Seek(size - 1, SeekOrigin.Begin);
                                        byte[] lastByteBuf = new byte[1];
                                        int read = await stream.ReadAsync(lastByteBuf, 0, 1);
                                        if (read > 0 && lastByteBuf[0] == 10) // '\n'
                                        {
                                            endsWithNewline = true;
                                        }

                                        stream.Seek(0, SeekOrigin.Begin);
                                        using (var reader = new StreamReader(stream, Encoding.UTF8))
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

                                        if (endsWithNewline)
                                        {
                                            linesCount++;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                linesCount = 1;
                            }
                            results[currentFile] = new FileStatResult { Size = size, Width = width, Lines = linesCount };
                        }
                        else
                        {
                            results[currentFile] = new FileStatResult { Size = 0, Width = 0, Lines = 0 };
                        }
                    }
                    catch
                    {
                        results[currentFile] = new FileStatResult { Size = 0, Width = 0, Lines = 0 };
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            return new Dictionary<string, FileStatResult>(results);
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
            var provider = new DiskFileStatsProvider();
            return provider.ComputeFileStatsAsync(repoRoot, files, concurrency);
        }
    }

    public class FileStatResult
    {
        public long Size { get; set; }
        public int Width { get; set; }
        public int Lines { get; set; }
    }
}
