using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace rHXLib;

internal sealed class OidcTokenProvider : ITokenProvider
{
    private readonly AuthOptions _options;
    private readonly string[] _scopes;
    private IPublicClientApplication? _app;

    public OidcTokenProvider(AuthOptions options)
    {
        _options = options;
        _scopes = options.Scopes is { Length: > 0 } ? options.Scopes : new[] { "openid", "profile", "email", "offline_access" };
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Authority) || string.IsNullOrWhiteSpace(_options.ClientId))
            return null; // not configured; caller can ignore

        _app ??= PublicClientApplicationBuilder
            .Create(_options.ClientId!)
            .WithAuthority(_options.Authority!)
            .WithDefaultRedirectUri()
            .Build();

        try
        {
            var accounts = await _app.GetAccountsAsync().ConfigureAwait(false);
            var first = accounts.FirstOrDefault();
            if (first != null)
            {
                try
                {
                    var silent = await _app.AcquireTokenSilent(_scopes, first).ExecuteAsync(ct).ConfigureAwait(false);
                    return silent.AccessToken;
                }
                catch
                {
                    // ignore and fall through to device code
                }
            }

            if (_options.UseDeviceCode)
            {
                var result = await _app.AcquireTokenWithDeviceCode(_scopes, dc =>
                {
                    // The host app can hook into this by providing its own UI callbacks later if desired.
                    Console.WriteLine(dc.Message);
                    return Task.CompletedTask;
                }).ExecuteAsync(ct).ConfigureAwait(false);
                return result.AccessToken;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}

