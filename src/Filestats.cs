using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic
{
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

        public static async Task<Dictionary<string, FileStatResult>> ComputeFileStatsAsync(
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

                            byte[] buffer = await File.ReadAllBytesAsync(fullPath);
                            if (!IsBinaryFile(buffer))
                            {
                                string content = Encoding.UTF8.GetString(buffer);
                                var fileLines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                                linesCount = fileLines.Length;
                                foreach (var line in fileLines)
                                {
                                    if (line.Length > width)
                                    {
                                        width = line.Length;
                                    }
                                }
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

    public class FileStatResult
    {
        public long Size { get; set; }
        public int Width { get; set; }
        public int Lines { get; set; }
    }
}
