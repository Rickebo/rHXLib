using System.Text;
using Newtonsoft.Json;

namespace rHXLib;

public sealed class NewtonsoftJsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerSettings _settings;

    public NewtonsoftJsonMessageSerializer(JsonSerializerSettings? settings = null)
    {
        _settings = settings ?? new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    public string ContentType => "application/json";

    public byte[] Serialize<T>(T value)
    {
        var json = JsonConvert.SerializeObject(value!, _settings);
        return Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject<T>(json, _settings)!;
    }
}

