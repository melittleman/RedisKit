using Microsoft.Extensions.DependencyInjection;

namespace NRedisKit.DependencyInjection;

/// <inheritdoc />
internal sealed record DefaultRedisConnectionBuilder : IRedisConnectionBuilder
{
    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IServiceCollection Services { get; }

    /// <inheritdoc />
    public Action<RedisConnectionOptions>? Configure { get; }

    public DefaultRedisConnectionBuilder(
        string name,
        IServiceCollection services,
        Action<RedisConnectionOptions>? configure = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Services = services ?? throw new ArgumentNullException(nameof(services));

        Configure = configure;
    }
}
