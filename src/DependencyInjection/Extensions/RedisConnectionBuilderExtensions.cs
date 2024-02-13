using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;

using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.StackExchangeRedis;

using RedisKit.Diagnostics;
using RedisKit.Authentication;
using RedisKit.Messaging;
using RedisKit.Messaging.Constants;
using RedisKit.Messaging.Abstractions;
using RedisKit.Json.Converters;

namespace RedisKit.DependencyInjection.Extensions;

public static class RedisConnectionBuilderExtensions
{
    public static IRedisConnectionBuilder AddRedisDataProtection(
        this IRedisConnectionBuilder builder,
        IHostEnvironment env,
        Action<RedisDataProtectionOptions>? configure = null)
    {
        if (env is null) throw new ArgumentNullException(nameof(env));
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        RedisDataProtectionOptions dpOptions = new();
        configure?.Invoke(dpOptions);

        RedisConnectionOptions connectionOptions = new();
        builder.Configure?.Invoke(connectionOptions);

        connectionOptions.ClientName ??= env.ApplicationName;

        // This prevents any other application in different environments
        // from being able to access protected payloads encrypted by this application.
        // NOTE: This would need to be set to the same value if we want to share payloads between apps.
        // See: https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-6.0#setapplicationname
        string applicationIsolation = dpOptions.ApplicationIsolation is null
            ? $"{connectionOptions.ClientName}({env.EnvironmentName})"
            : $"{dpOptions.ApplicationIsolation}({env.EnvironmentName})";

        // TODO: Key encryption at rest is unfortunately still not working due to the below exception:
        // System.Security.Cryptography.CryptographicException: Unable to retrieve the decryption key
        // It currently requires that the X.509 Certificate be installed on the machine itself, however
        // we ideally want to be able to resolve from AWS SecretsManager anyway.

        builder.Services.AddDataProtection()
            .SetApplicationName(applicationIsolation);
            //.ProtectKeysWithCertificate();

        builder.Services.AddOptions<KeyManagementOptions>().Configure<IRedisConnectionProvider, ILogger<IRedisConnectionBuilder>>((options, provider, logger) =>
        {
            IRedisConnection connection = provider.GetRequiredConnection(builder.Name);

            logger.LogInformation("Using Redis endpoint {Endpoint} for Data Protection keys persistence.", connection.Endpoints);

            logger.LogInformation("Using application isolation {NameAndEnvironment} for Data Protection.", applicationIsolation);

            options.XmlRepository = new RedisXmlRepository(() => connection.Db, new RedisKey(dpOptions.KeyName));
        });

        return builder;
    }

    /// <summary>
    ///     Adds a transient <see cref="RedisTicketStore" /> to the Dependency Injection container,
    ///     based on the named Redis connection container within this <paramref name="builder"/>.
    /// </summary>
    /// <remarks>
    ///     This should only be called once during startup based on the named Redis connection
    ///     you wish to use. If called multiple times, only the last will be registered.
    /// </remarks>
    /// <param name="builder">
    ///     The <see cref="IRedisConnectionBuilder"/> that contains the named
    ///     Redis connection to use for Authentication Ticket storage.
    /// </param>
    /// <returns>
    ///     The modified <paramref name="builder"/> to be used for chaining further configuration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when the <paramref name="builder"/> is null.
    /// </exception>
    public static IRedisConnectionBuilder AddRedisTicketStore(
        this IRedisConnectionBuilder builder,
        Action<RedisAuthenticationTicketOptions>? configure = null)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        RedisAuthenticationTicketOptions ticketOptions = new();
        configure?.Invoke(ticketOptions);

        builder.Services.TryAddTransient<ITicketStore>(s =>
        {
            IRedisConnectionProvider provider = s.GetRequiredService<IRedisConnectionProvider>();
            IOptionsMonitor<RedisJsonOptions> optionsMonitor = s.GetRequiredService<IOptionsMonitor<RedisJsonOptions>>();

            IRedisConnection connection = provider.GetRequiredConnection(builder.Name);
            RedisJsonOptions jsonOptions = optionsMonitor.Get(builder.Name);

            return new RedisTicketStore(connection, ticketOptions, jsonOptions);
        });

        builder.Services.AddOptions<CookieAuthenticationOptions>(ticketOptions.CookieSchemeName).Configure<ITicketStore>((options, store) =>
        {
            options.SessionStore = store;
        });

        // Whilst this isn't technically required because the RedisTicketStore is hard coded
        // to use this converter, it seems sensible to include here as a default in case we want 
        // the user to be able to override in the future.

        return builder.ConfigureRedisJson(options =>
        {
            options.Serializer.Converters.Add(new AuthenticationTicketJsonConverter());
        });
    }

    public static IRedisConnectionBuilder ConfigureRedisJson(
        this IRedisConnectionBuilder builder,
        Action<RedisJsonOptions> configure)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        builder.Services.Configure(builder.Name, configure);

        return builder;
    }

    internal static IRedisConnectionBuilder ConfigureRedisConnection(
        this IRedisConnectionBuilder builder,
        Action<RedisConnectionOptions>? configure = null)
    {
        // Configure could be null when allowing it to just
        // use the 'default' connection e.g. "localhost:6379"
        if (configure is not null)
        {
            // Adds the named configuration to be available within the DI container
            // using either the IOptionSnapshot or IOptionsMonitor .Get(name) methods.
            builder.Services.Configure(builder.Name, configure);
        }

        return builder;
    }

    /// <summary>
    ///     Adds an <see cref="IHealthCheck" /> implementation of <see cref="RedisHealthCheck" />
    ///     that uses this <paramref name="builder"/> to retrieve the Redis connection to check against.
    /// </summary>
    /// <param name="builder">
    ///     The <see cref="IRedisConnectionBuilder" /> that contains the Name of Redis connection to Health Check for.
    /// </param>
    /// <returns>The modified <see cref="IRedisConnectionBuilder" /> to be further chained to.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static IRedisConnectionBuilder AddRedisHealthCheck(this IRedisConnectionBuilder builder)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        builder.Services
            .AddHealthChecks()
            .Add(new HealthCheckRegistration($"Redis:{builder.Name}", sp =>
            {
                IRedisConnectionProvider provider = sp.GetRequiredService<IRedisConnectionProvider>();
                ILogger<RedisHealthCheck> logger = sp.GetRequiredService<ILogger<RedisHealthCheck>>();

                IRedisConnection connection = provider.GetRequiredConnection(builder.Name);

                return new RedisHealthCheck(connection, logger);

            }, HealthStatus.Unhealthy, new[] { "redis" }));

        return builder;
    }

    #region Redis Streams

    public static IRedisConnectionBuilder AddRedisStreamsConsumer(
        this IRedisConnectionBuilder builder,
        Action<RedisMessagingOptions>? configure = null)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        RedisMessagingOptions messagingOptions = new();
        configure?.Invoke(messagingOptions);

        // TODO: Can we use the generic argument to specify the named connection based on different message types?
        builder.Services.Configure<RedisMessagingOptions>(MessagingOptionsDefaults.ConsumerName, options =>
        {
            options.BrokerConnectionName = messagingOptions.BrokerConnectionName ?? builder.Name;
            options.ConsumerGroupName = messagingOptions.ConsumerGroupName;
        });

        // TODO: Investigate how this could work to have multiple 'keyed' Consumers in one application?
        builder.Services.TryAddKeyedTransient(typeof(IMessageConsumer<>), builder.Name, typeof(RedisStreamsConsumer<>));
        builder.Services.TryAddTransient(typeof(IMessageConsumer<>), typeof(RedisStreamsConsumer<>));

        return builder;
    }

    public static IRedisConnectionBuilder AddRedisStreamsProducer(
        this IRedisConnectionBuilder builder,
        Action<RedisMessagingOptions>? configure = null)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        RedisMessagingOptions messagingOptions = new();
        configure?.Invoke(messagingOptions);

        // TODO: Can we use the generic argument to specify the named connection based on different message types?
        builder.Services.Configure<RedisMessagingOptions>(MessagingOptionsDefaults.ProducerName, options =>
        {
            options.BrokerConnectionName = messagingOptions.BrokerConnectionName ?? builder.Name;
        });

        // TODO: Investigate how this could work to have multiple 'keyed' Producers in one application?
        builder.Services.TryAddKeyedTransient(typeof(IMessageProducer<>), builder.Name, typeof(RedisStreamsProducer<>));
        builder.Services.TryAddTransient(typeof(IMessageProducer<>), typeof(RedisStreamsProducer<>));

        return builder;
    }

    public static IRedisConnectionBuilder AddRedisOpenTelemetry(
        this IRedisConnectionBuilder builder,
        Action<StackExchangeRedisInstrumentationOptions> configure)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        builder.Services.AddOpenTelemetry().WithTracing(b => b.AddRedisInstrumentation($"Redis:{builder.Name}", null, configure).ConfigureRedisInstrumentation((s, i) =>
        {
            IRedisConnectionProvider provider = s.GetRequiredService<IRedisConnectionProvider>();
            IRedisConnection connection = provider.GetRequiredConnection(builder.Name);
            
            i.AddConnection(connection.Multiplexer);
        }));

        return builder;
    }

    #endregion
}
