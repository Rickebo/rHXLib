using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using rHXLib.Tests.Fakes;
using Xunit;

namespace rHXLib.Tests;

public class PublishSubscribeTests
{
    [Fact]
    public async Task Publish_SendsBinaryPayload_FromSerializer()
    {
        var ws = new FakeWebSocketFactory();
        var opts = Options.Create(new RhxLibOptions
        {
            ApiBaseUrl = new Uri("https://example.com/api"),
            Serializer = new NewtonsoftJsonMessageSerializer(),
            Auth = new AuthOptions { AccessToken = "abc" }
        });

        var client = new RhxPubSubClient(opts, NullLogger<RhxPubSubClient>.Instance, ws);
        var obj = new Sample { Name = "n", Count = 7 };

        await client.PublishAsync("topic1", obj);

        var sent = ws.LastConnection.TryDequeueSent();
        sent.Should().NotBeNull();
        var json = Encoding.UTF8.GetString(sent!);
        json.Should().Contain("\"Name\":\"n\"");
        json.Should().Contain("\"Count\":7");
    }

    #if NETFRAMEWORK
    [Fact]
    public async Task Subscribe_Deserializes_Payload_And_Invokes_Handler()
    {
        var ws = new FakeWebSocketFactory();
        var opts = Options.Create(new RhxLibOptions
        {
            ApiBaseUrl = new Uri("https://example.com/api"),
            Serializer = new NewtonsoftJsonMessageSerializer(),
            Auth = new AuthOptions { AccessToken = "abc" }
        });

        var client = new RhxPubSubClient(opts, NullLogger<RhxPubSubClient>.Instance, ws);
        var tcs = new TaskCompletionSource<Sample>(TaskCreationOptions.RunContinuationsAsynchronously);

        var sub = await client.SubscribeAsync<Sample>("topic2", (s, ctx) =>
        {
            tcs.TrySetResult(s);
            return Task.CompletedTask;
        });

        // Wait for factory to create the connection used by the subscriber
        var initial = ws.LastConnection;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (ReferenceEquals(initial, ws.LastConnection) && sw.Elapsed < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(25);
        }

        var payload = Encoding.UTF8.GetBytes("{\"Name\":\"hello\",\"Count\":3}");
        var b64 = Convert.ToBase64String(payload);
        var sentAt = DateTimeOffset.UtcNow.ToString("o");
        var json = $"{{\"header\":{{\"topic\":\"topic2\",\"username\":\"u1\",\"sentAtUtc\":\"{sentAt}\"}},\"payload\":\"{b64}\"}}";
        var connRef = ws.LastConnection;
        connRef.EnqueueText(json);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(tcs.Task);
        var received = await tcs.Task;
        received.Name.Should().Be("hello");
        received.Count.Should().Be(3);

        sub.Dispose();
    }
    #endif

    private sealed class Sample { public string Name { get; set; } = string.Empty; public int Count { get; set; } }
}
