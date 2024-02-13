using System.Collections.Concurrent;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace RedisKit.DependencyInjection;

/// <inheritdoc />
internal sealed record DefaultRedisConnectionProvider : IRedisConnectionProvider
{
    private readonly ConcurrentDictionary<string, RedisConnection> RedisConnections = new();

    private readonly IOptionsMonitor<RedisConnectionOptions> _options;
    private readonly ILoggerFactory _loggers;
    private readonly IHostEnvironment _env;

    // IOptionsMonitor<RedisConnectionOptions> will allow
    // us to re-load and re-connect to Redis during
    // runtime if the Connection String changes.
    // Although the limitation here right now,
    // is that we are not currently polling AWS
    // to see whether the secret updates.
    public DefaultRedisConnectionProvider(
        IOptionsMonitor<RedisConnectionOptions> options,
        ILoggerFactory loggers,
        IHostEnvironment env)
    {
        _options = options;
        _loggers = loggers;
        _env = env;
    }

    /// <inheritdoc />
    public IRedisConnection GetRequiredConnection(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (RedisConnections.TryGetValue(name, out RedisConnection? connection) && connection is not null)
        {
            return connection;
        }

        // Will 'Lazily' add these Redis connections to the internal
        // dictionary if a request for it has been made but it was not 
        // yet initialized. This seems to happen a lot with .NET DI,
        // especially when services use the .PostConfigure method.

        RedisConnectionOptions? options = _options.Get(name);
        if (options is not null)
        {
            // TODO: Might be quite nice here to first check whether we have a connection already created
            // to the same Redis server despite it being given a different name? Will be very useful either
            // locally or when in the Dev environment, as these will often share a single server for caching,
            // messaging pub/sub and persistent requirements.

            connection = new RedisConnection(_loggers, _env, options);

            if (AddConnection(name, connection)) return connection;
        }

        throw new InvalidOperationException($"Unknown Redis connection name '{name}'.");
    }

    /// <summary>
    ///     Adds the provided <see cref="RedisConnection" /> to the internal 
    ///     <see cref="ConcurrentDictionary{TKey, TValue}" /> under the key <paramref name="name" />.
    /// </summary>
    /// <param name="name">
    ///     The User-friendly <see cref="string"/> "name" to key this <see cref="RedisConnection" /> off.
    /// </param>
    /// <param name="connection">
    ///     The Redis connection to store under the <paramref name="name" />.
    /// </param>
    /// <returns>
    ///     A <see cref="bool"/> to indicate whether the addition was successful or not.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the <see cref="ConcurrentDictionary{TKey, TValue}" /> already contains
    ///     a key matching <paramref name="name" />.
    /// </exception>
    private bool AddConnection(string name, RedisConnection connection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(connection);

        if (RedisConnections.ContainsKey(name))
            throw new InvalidOperationException($"Redis connection name '{name}' already exists!");

        return RedisConnections.TryAdd(name, connection);
    }
}
