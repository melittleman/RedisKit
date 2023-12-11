namespace NRedisKit.DependencyInjection.Options;

// TODO: We may eventually need to look at creating specific options
// betwee Producers and Consumers as currently they both use the same IOptions
// model. It gets potentially more complicated if/when we need to point the same
// Message Producer or Consumer logic to a completely separate server...
// e.g. A internal Redis server for private communication vs a public one?
public sealed record RedisMessagingOptions
{
    /// <summary>
    ///     The name of the Redis connection to use for the message broker.
    /// </summary>
    /// <remarks>
    ///     If not specified, will default to the
    ///     same name as the chained connection builder.
    /// </remarks>
    public string? BrokerConnectionName { get; set; }

    /// <summary>
    ///     The name of Consumer Group to receive messages in.
    /// </summary>
    /// <remarks>
    ///     If not specified, will default to IHostEnvironment.ApplicationName
    /// </remarks>
    public string? ConsumerGroupName { get; set; }
}
