using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RedisKit.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds an instance of <see cref="RedisJsonOptions"/> to the DI container.
    /// </summary>
    /// <param name="services">The DI container Services collection.</param>
    /// <returns>
    ///     The <paramref name="services"/> to be used for further configuration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="services" /> is null.
    /// </exception>
    public static IServiceCollection ConfigureRedisJson(
        this IServiceCollection services,
        Action<RedisJsonOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        return services;
    }

    /// <summary>
    ///     Adds a named Redis connection to the DI services collection within the singleton <see cref="DefaultRedisConnectionProvider" />.
    /// </summary>
    /// <remarks>
    ///     This can then be retrieved at a later time by using the
    ///     <c><see cref="IRedisConnectionProvider"/>.GetRequiredConnection("name");</c> method from DI.
    /// </remarks>
    /// <param name="services">The application service collection within the Dependency Injection container.</param>
    /// <param name="name">The unique name for this <see cref="RedisConnection"/> connection.</param>
    /// <param name="configure">The configure action used to provide configuration to the connection.</param>
    /// <returns>
    ///     A new <see cref="IRedisConnectionBuilder"/> that can be used to chain
    ///     configuration calls via the fluent builder API.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when either <paramref name="services"/> or <paramref name="configure"/> or <paramref name="name"/> are null.
    /// </exception>
    public static IRedisConnectionBuilder AddRedisConnection(
        this IServiceCollection services,
        string name,
        Action<RedisConnectionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(services);

        // Required dependencies
        services.AddOptions();
        services.AddLogging();

        // We could have multiple Redis Connections added to the DI container.
        // So 'TryAdd' will only add once, if it does not already exist.
        services.TryAddSingleton<IRedisConnectionProvider, DefaultRedisConnectionProvider>();

        // Adds a default transient Redis connection implementation based on the last registered name.
        // This would be useful for clients with only a single named connection as they can then
        // utilize the RedisConnection from DI rather than requesting from the factory directly.
        services.AddTransient(sp =>
        {
            IRedisConnectionProvider factory = sp.GetRequiredService<IRedisConnectionProvider>();

            return factory.GetRequiredConnection(name);
        });

        DefaultRedisConnectionBuilder builder = new(name, services, configure);

        return builder.ConfigureRedisConnection(configure);
    }
}
