using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NRedisKit.DependencyInjection.Extensions;

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
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        services.Configure(configure);

        return services;
    }

    /// <summary>
    ///     Adds an instance of <see cref="RedisJsonOptions"/> to the DI container,
    ///     based on the provided <see cref="JsonSerializerOptions"/>.
    /// </summary>
    /// <param name="services">The DI container Services collection.</param>
    /// <param name="options">
    ///     Allows a fully customizable <see cref="JsonSerializerOptions"/>
    ///     instance to be passed to the Redis JSON serilializer.
    /// </param>
    /// <returns>
    ///     The <paramref name="services"/> to be used for further configuration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="services" /> is null.
    /// </exception>
    public static IServiceCollection ConfigureRedisJson(
        this IServiceCollection services,
        JsonSerializerOptions jsonSerializer)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (jsonSerializer is null) throw new ArgumentNullException(nameof(jsonSerializer));

        services.Configure<RedisJsonOptions>(options =>
        {
            options.Serializer = jsonSerializer;
        });

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
    /// <param name="name">The unique name for this <see cref="RedisContext"/> connection.</param>
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
        if (name is null) throw new ArgumentNullException(nameof(name));
        if (services is null) throw new ArgumentNullException(nameof(services));

        // Required dependencies
        services.AddOptions();
        services.AddLogging();

        // We could have multiple Redis Connections added to the DI container.
        // So 'TryAdd' will only add once, if it does not already exist.
        services.TryAddSingleton<IRedisConnectionProvider, DefaultRedisConnectionProvider>();
        services.TryAddTransient<IRedisClientFactory, DefaultRedisClientFactory>();

        // Adds a default transient Redis Client implementation based on the last registered name.
        // This would be useful for clients with only a single named connection as they can then
        // utilize the RedisClient from DI rather than requesting from the factory directly.
        services.AddTransient(sp =>
        {
            IRedisClientFactory factory = sp.GetRequiredService<IRedisClientFactory>();

            return factory.CreateClient(name);
        });

        DefaultRedisConnectionBuilder builder = new(name, services, configure);

        return builder.ConfigureRedisConnection(configure);
    }
}
