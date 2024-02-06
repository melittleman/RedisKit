using Microsoft.Extensions.DependencyInjection;

namespace RedisKit.DependencyInjection.Abstractions;

/// <summary>
///     A fluent helper when building Redis connections that
///     allows chaining together of multiple extensions.
/// </summary>
public interface IRedisConnectionBuilder
{
    /// <summary>
    ///     The User-friendly name for this Redis connection.
    /// </summary>
    /// <example>
    ///     e.g. "enterpise-cache" or "OnPrem Redis"
    /// </example>
    string Name { get; }

    /// <summary>
    ///     The services currently being registered within the
    ///     applications Dependency Injection container.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    ///     The configure action that contains the specified
    ///     configuration for this Redis connection instance.
    /// </summary>
    Action<RedisConnectionOptions>? Configure { get; }
}
