using System.Text.Json;

namespace RedisKit.DependencyInjection.Options;

public sealed record RedisJsonOptions
{
    public JsonSerializerOptions Serializer { get; } = new();
}
