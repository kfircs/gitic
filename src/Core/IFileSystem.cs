using System.IO;

namespace Gitic;

public interface IFileSystem
{
    bool FileExists(string path);
    long GetFileSize(string path);
    Stream OpenRead(string path);
}
