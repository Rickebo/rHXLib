using System.Text;
using FluentAssertions;
using Xunit;

namespace rHXLib.Tests;

public class SerializationTests
{
    [Fact]
    public void Newtonsoft_Roundtrips_Custom_Class()
    {
        var ser = new NewtonsoftJsonMessageSerializer();
        var obj = new Sample { Id = 42, Name = "test" };
        var bytes = ser.Serialize(obj);
        Encoding.UTF8.GetString(bytes).Should().Contain("\"Id\":42");
        var back = ser.Deserialize<Sample>(bytes);
        back.Should().BeEquivalentTo(obj);
    }

    private sealed class Sample { public int Id { get; set; } public string Name { get; set; } = string.Empty; }
}
