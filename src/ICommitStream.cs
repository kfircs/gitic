using System.Collections.Generic;
using System.Threading.Tasks;

namespace Gitic
{
    public interface ICommitStream
    {
        Task<List<GitCommitRecord>> ExtractHistoryAsync(GitHistoryExtractorOptions? options = null);
    }
}