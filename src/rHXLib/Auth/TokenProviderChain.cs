using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace rHXLib;

internal sealed class TokenProviderChain : ITokenProvider
{
    private readonly IReadOnlyList<ITokenProvider> _providers;

    public TokenProviderChain(params ITokenProvider[] providers)
    {
        _providers = providers;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        foreach (var p in _providers)
        {
            var token = await p.GetAccessTokenAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token)) return token;
        }
        return null;
    }
}

