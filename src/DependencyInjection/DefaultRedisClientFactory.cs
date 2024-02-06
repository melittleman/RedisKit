using Microsoft.Extensions.Options;

namespace RedisKit.DependencyInjection;

internal sealed record DefaultRedisClientFactory : IRedisClientFactory
{
    private readonly ILoggerFactory _loggers;
    private readonly RedisJsonOptions _jsonOptions;
    private readonly IRedisConnectionProvider _provider;

    public DefaultRedisClientFactory(
        ILoggerFactory loggers,
        IRedisConnectionProvider provider,
        IOptions<RedisJsonOptions> jsonOptions)
    {
        _loggers = loggers ?? throw new ArgumentNullException(nameof(loggers));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _jsonOptions = jsonOptions?.Value ?? throw new ArgumentNullException(nameof(jsonOptions));
    }

    public RedisClient CreateClient(string name)
    {
        IRedisContext context = _provider.GetRequiredConnection(name);

        return new RedisClient(_loggers, context, _jsonOptions);
    }
}
