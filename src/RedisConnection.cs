using System.Threading;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace RedisKit;

/// <inheritdoc cref="IRedisConnection"/>
public sealed record RedisConnection : IRedisConnection
{
    private readonly ILogger<RedisConnection> _logger;
    private readonly RedisConnectionOptions _options;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private Lazy<IConnectionMultiplexer>? _lazyConnection;

    /// <inheritdoc />
    public IConnectionMultiplexer Multiplexer
    {
        get
        {
            if (_lazyConnection is null || _lazyConnection.Value.IsConnected is false)
            {
                // We can attempt to re-connect here as an extra level of redundancy.
                // Internally the ConnectionMultiplexer will be provided a retry policy anyway.
                CreateConnection();
            }

            return
            (
                _lazyConnection is not null && _lazyConnection.Value.IsConnected
                    ? _lazyConnection.Value
                    : null

            ) ?? throw new InvalidOperationException("Redis multiplexer is not connected. We have lost connection to the server...");
        }
    }

    /// <inheritdoc />
    public IDatabase Db => Multiplexer.GetDatabase();

    /// <inheritdoc />
    public ISubscriber Sub => Multiplexer.GetSubscriber();

    public IServer Server => Multiplexer.GetServer(_options.HostnameAndPort);

    /// <inheritdoc />
    public string? Endpoints { get; private set; }

    /// <inheritdoc />
    public string ClientName { get; }

    #region Constructors

    internal RedisConnection(
        IHostEnvironment env,
        IOptions<RedisConnectionOptions> options) : this(env, options.Value) { }

    internal RedisConnection(
        IHostEnvironment env,
        RedisConnectionOptions options) : this(new NullLoggerFactory(), env, options) { }

    internal RedisConnection(
        ILoggerFactory loggers,
        IHostEnvironment env,
        IOptions<RedisConnectionOptions> options) : this(loggers, env, options.Value) { }

    internal RedisConnection(
        ILoggerFactory loggers,
        IHostEnvironment env,
        RedisConnectionOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = loggers.CreateLogger<RedisConnection>();

        // TODO: This is also being checked and assigned in the Data Protection
        // builder extensions... how can we centralize this?
        _options.ClientName ??= env.ApplicationName;

        // Example:
        // MY-DESKTOP/MyCompany.MyProject(Development)
        ClientName = $"{Environment.MachineName}/{_options.ClientName}({env.EnvironmentName})";

        _logger.LogInformation("Using Redis connection client name: {Client}", ClientName);

        CreateConnection();
    }

    #endregion

    private void CreateConnection()
    {
        _connectionLock.Wait();

        try
        {
            // If another thread has entered the lock previously,
            // this connection may now be created once it is released.
            if (_lazyConnection is not null &&
                (_lazyConnection.Value.IsConnected || _lazyConnection.Value.IsConnecting)) return;

            ConfigurationOptions configuration = GetConfigurationOptions();

            // Close any existing connection, allowing commands to complete.
            // Use the field rather than the property here, as the 'getter'
            // will attempt to re-create the multiplexer if not connected.
            _lazyConnection?.Value.Close(allowCommandsToComplete: true);

            // Be Lazy, but be wary of initialization exceptions...
            // 'PublicationOnly' seems to be the general recommeneded approach here.
            // See: https://theburningmonk.com/2013/04/be-lazy-but-be-ware-of-initialization-exception
            _lazyConnection = new Lazy<IConnectionMultiplexer>(() =>
            {
                _logger.LogInformation("Attempting connection to Redis endpoints: {Endpoints}", Endpoints);

                // TODO: Investigate ConnectionMultiplexer.ConnectAsync() is this better than the below?
                // Does the current 'sync' version .Connect() block any threads during startup for example.
                return ConnectionMultiplexer.Connect(configuration);

            }, LazyThreadSafetyMode.PublicationOnly);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to connect to Redis. {Message}", ex.Message);
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private ConfigurationOptions GetConfigurationOptions()
    {
        ConfigurationOptions configuration;

        if (_options.ConnectionString is not null)
        {
            if (_options.ConnectionString.StartsWith("redis://"))
            {
                // e.g: redis://username:password@redis.com:1234
                configuration = new ConfigurationOptions().FromCli(_options.ConnectionString);
            }
            else
            {
                // e.g: redis0:6380,redis1:6380,allowAdmin=true,ssl=false
                configuration = ConfigurationOptions.Parse(_options.ConnectionString);
            }
        }
        else
        {
            // Fallback to the explicitly provided values
            configuration = new ConfigurationOptions()
            {
                EndPoints = { _options.HostnameAndPort },
                Password = _options.Password,

                Ssl = _options.UseSsl,
                AllowAdmin = _options.AllowAdmin,
            };
        }

        Endpoints = string.Join(";", configuration.EndPoints);

        _logger.LogInformation("Configuring Redis endpoints: {Endpoints}", Endpoints);

        // We may need to overwrite the Client Name once again as the string version
        // won't contain certain information required such as the Machine Name.
        configuration.ClientName = ClientName;

        // TODO: What else should we be overwriting?

        // Allows the multiplexer to retry connecting after a failure.
        configuration.AbortOnConnectFail = false;

        // The 'Config' channel is used to Subscribe to events from a cluster such
        // as a failover notification from the Master node. We can disable it by setting
        // the channel to an empty string to save on the number of connected clients.
        // However, once in a Production scenario we may actually want to use this...
        // See: https://github.com/StackExchange/StackExchange.Redis/issues/1228
        // And: https://stackoverflow.com/questions/28145865/stackexchange-redis-why-does-connectionmultiplexer-connect-establishes-two-clien
        configuration.ConfigurationChannel = string.Empty;
        configuration.CommandMap = CommandMap.Default;

        if (_options.UseSubscriber is false)
        {
            configuration.CommandMap = CommandMap.Create(["SUBSCRIBE"], false);
        }

        // Retries in exponential intervals between 2 and 10 seconds
        configuration.ReconnectRetryPolicy = new ExponentialRetry(3000);

        // Retry up to 5 times before we give up.
        configuration.ConnectRetry = 5;

        return configuration;
    }

    ~RedisConnection()
    {
        // Use the field rather than the property here, as the 'getter'
        // will attempt to re-create the multiplexer if not connected
        _lazyConnection?.Value.Dispose();
        _lazyConnection = null;

        _connectionLock.Dispose();
    }
}
