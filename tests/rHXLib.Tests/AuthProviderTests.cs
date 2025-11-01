using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace rHXLib.Tests;

public class AuthProviderTests
{
    [Fact]
    public async Task StaticToken_Returns_Value()
    {
        var p = new StaticTokenProvider("abc");
        var t = await p.GetAccessTokenAsync();
        t.Should().Be("abc");
    }

    [Fact]
    public async Task LauncherFile_Reads_access_token_From_Json()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(tmp, "{\"access_token\":\"tok123\"}");
            var p = new LauncherFileTokenProvider(tmp);
            var t = await p.GetAccessTokenAsync();
            t.Should().Be("tok123");
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }
}
