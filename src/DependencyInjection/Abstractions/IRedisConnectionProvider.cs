using System.Collections.Concurrent;

namespace NRedisKit.DependencyInjection.Abstractions;

/// <summary>
///     A singleton container around multiple pooled Redis connections.
/// </summary>
/// <remarks>
///     Internally keeps these <see cref="RedisContext" /> implementations
///     keyed by "name" in a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </remarks>
public interface IRedisConnectionProvider
{
    /// <summary>
    ///     Retrive the pooled Redis connection context from the
    ///     <see cref="ConcurrentDictionary{TKey, TValue}"/> by <paramref name="name"/>.
    /// </summary>
    /// <param name="name">
    ///     The friendly "name" <see cref="string" /> for the Redis connection you wish to retrieve.
    ///     <para>The MUST be the same "name" that was used to create the Redis connection during startup.</para>
    /// </param>
    /// <returns>An existing <see cref="IRedisContext" /> if found.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if we are unable to find a mathing Redis Connection with <paramref name="name" />.
    /// </exception>
    IRedisContext GetRequiredConnection(string name);
}
