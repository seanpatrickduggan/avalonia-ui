using System.Threading;
using System.Threading.Tasks;

namespace FileProcessor.Core.App;

public interface IApplicationHost
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
