using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace rHXLib;

internal sealed class LauncherFileTokenProvider : ITokenProvider
{
    private readonly string _path;

    public LauncherFileTokenProvider(string? explicitPath = null)
    {
        _path = explicitPath ?? GetDefaultPath();
    }

    private static string GetDefaultPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "rHX", "launcher", "auth.json");
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_path)) return null;
            using var fs = File.OpenRead(_path);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            var json = await sr.ReadToEndAsync().ConfigureAwait(false);
            var jo = JObject.Parse(json);
            var token = (string?)jo["access_token"] ?? (string?)jo["token"];
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch
        {
            return null;
        }
    }
}

