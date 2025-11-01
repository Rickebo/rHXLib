using System.Threading;
using System.Threading.Tasks;

namespace rHXLib;

public interface ITokenProvider
{
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
}

