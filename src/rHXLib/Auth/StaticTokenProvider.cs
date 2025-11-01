using System.Threading;
using System.Threading.Tasks;

namespace rHXLib;

internal sealed class StaticTokenProvider : ITokenProvider
{
    private readonly string? _token;

    public StaticTokenProvider(string? token)
    {
        _token = token;
    }

    public Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
        => Task.FromResult(_token);
}

