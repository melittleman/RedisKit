using System.Threading.Tasks;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

using NRedisKit.Messaging.Abstractions;
using NRedisKit.Messaging.Constants;

namespace NRedisKit.Messaging;

/// <summary>
///     See <c>https://stackexchange.github.io/StackExchange.Redis/Streams</c>
/// </summary>
/// <typeparam name="TMessage">
///     The Message type to produce.
/// </typeparam>
public sealed record RedisStreamsProducer<TMessage> : IMessageProducer<TMessage> where TMessage : class
{
    private readonly IRedisContext _redis;
    private readonly ILogger<RedisStreamsProducer<TMessage>> _logger;

    private IDatabase Db => _redis.Db;

    // This is a 'legacy' constructor for the .NET Framework
    // applications that do not have dependence injection.
    // They will have to create their own Redis context and
    // pass it in, and will not have access to logging.
    public RedisStreamsProducer(IRedisContext redis)
    {
        _logger = new NullLogger<RedisStreamsProducer<TMessage>>();
        _redis = redis;
    }

    public RedisStreamsProducer(
        IRedisConnectionProvider provider,
        ILogger<RedisStreamsProducer<TMessage>> logger,
        IOptionsMonitor<RedisMessagingOptions> optionsMonitor)
    {
        _logger = logger;

        RedisMessagingOptions options = optionsMonitor.Get(MessagingOptionsDefaults.ProducerName);

        if (options.BrokerConnectionName is null)
            throw new InvalidOperationException("Redis streams consumer has no broker connection");

        _redis = provider.GetRequiredConnection(options.BrokerConnectionName);
    }

    /// <inheritdoc />
    public async Task<bool> SendAsync(string streamName, TMessage message)
    {
        try
        {
            RedisValue result = await Db.StreamAddAsync(streamName, message.ToNameValueEntries());

            return result.IsNull is false && result.HasValue;
        }
        catch (Exception ex)
        {
            _logger.LogError("{Service} - {Method}: Caught exception with message: {Message}",
                nameof(RedisStreamsProducer<TMessage>),
                nameof(SendAsync),
                ex.Message);

            // Opting for a throw here rather than a a 'return false'
            // because we have legacy .NET Framework clients using this
            // library who do not have access to DI and therefore no ILogger.

            // At least if we throw here we can ensure we catch higher up the
            // chain and log to the Windows Event Viewer in the case of IIS.

            // TODO: Still not an ideal approach so I would rather revert this
            // back later on, so we do not need to 'doubly' wrap a try... catch.
            throw;
        }
    }
}
