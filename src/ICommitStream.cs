using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gitic;

public interface ICommitStream
{
    Task<List<GitCommitRecord>> ExtractHistoryAsync(GitHistoryExtractorOptions? options = null, CancellationToken cancellationToken = default);
}