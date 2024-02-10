namespace RedisKit.Abstractions;

/// <summary>
///     <para>The Redis connection.</para>
///     <para>
///         This MUST be available as a singleton to a unque Redis instance in order
///         for the <see cref="IConnectionMultiplexer"/> to be shared and re-used.
///     </para>
///     <para>
///         These are currently pooled and stored in <see cref="IRedisConnectionProvider"/>
///         to be used via the <c>.GetRequiredConnection("name")</c> method where "name"
///         is the User-friendly identifier used to initially create the connection.
///     </para>
/// </summary>
/// <remarks>
///     See: https://docs.redislabs.com/latest/rs/references/client_references/client_csharp/
/// </remarks>
public interface IRedisConnection
{
    /// <inheritdoc cref="IConnectionMultiplexer" />
    IConnectionMultiplexer Multiplexer { get; }

    /// <inheritdoc cref="IDatabase" />
    IDatabase Db { get; }

    /// <inheritdoc cref="ISubscriber" />
    ISubscriber Sub { get; }

    /// <inheritdoc cref="IServer" />
    IServer Server { get; }

    /// <summary>
    ///     The Redis 'stringified' endpoint information.
    ///     Should NOT include credential (password) information.
    ///     Can potentially contain multiple when in a clustered
    ///     environment so may be semi-colon separated.
    /// </summary>
    string? Endpoints { get; }

    /// <summary>
    ///     The User-friendly display name given to identify this
    ///     Redis connection when using "CLIENT LIST" from the Server.
    /// </summary>
    string ClientName { get; }
}
