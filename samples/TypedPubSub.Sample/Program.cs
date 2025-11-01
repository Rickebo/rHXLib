using Microsoft.Extensions.DependencyInjection;
using rHXLib;

var services = new ServiceCollection()
    .AddRhxLib(o =>
    {
        // Replace with your API base URL if different
        o.ApiBaseUrl = new Uri("https://rhx.rickebo.com/api");
        // Optional: o.Auth.AccessToken = "<token>";
    });

var sp = services.BuildServiceProvider();
var client = sp.GetRequiredService<IRhxPubSubClient>();

var topic = args.Length > 0 ? args[0] : "sample";

var sub = await client.SubscribeAsync<Demo>(topic, (msg, ctx) =>
{
    Console.WriteLine($"[{ctx.TimestampUtc:o}] {ctx.Username}: {msg.Text}");
    return Task.CompletedTask;
});

Console.WriteLine($"Subscribed to '{topic}'. Type lines to publish. Ctrl+C to exit.");

string? line;
while ((line = Console.ReadLine()) != null)
{
    await client.PublishAsync(topic, new Demo { Text = line });
}

sub.Dispose();

public sealed class Demo { public string Text { get; set; } = string.Empty; }

