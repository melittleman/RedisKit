using System.Text.Json;

namespace NRedisKit.DependencyInjection.Options;

public sealed record RedisJsonOptions
{
    public JsonSerializerOptions Serializer { get; set; } = new JsonSerializerOptions();
}
