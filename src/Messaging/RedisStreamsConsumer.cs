using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

using NRedisKit.Messaging.Constants;
using NRedisKit.Messaging.Abstractions;

namespace NRedisKit.Messaging;

/// <summary>
///     See <c>https://stackexchange.github.io/StackExchange.Redis/Streams</c>
/// </summary>
/// <typeparam name="TMessage">
///     The Message type to consume.
/// </typeparam>
public sealed record RedisStreamsConsumer<TMessage> : IMessageConsumer<TMessage>
{
    private readonly IRedisContext _redis;
    private readonly ILogger<RedisStreamsConsumer<TMessage>> _logger;

    private readonly string _consumerGroup;
    private readonly string _consumerName;

    private string? _streamName;

    private IDatabase Db => _redis.Db;

    // This is a 'legacy' constructor for the .NET Framework
    // applications that do not have dependence injection.
    // They will have to create their own Redis context and
    // pass it in, and will not have access to logging.
    public RedisStreamsConsumer(
        string consumerGroup,
        IRedisContext redis)
    {
        _logger = new NullLogger<RedisStreamsConsumer<TMessage>>();

        _redis = redis;
        _consumerGroup = consumerGroup;
        _consumerName = $"{Environment.MachineName}-{typeof(TMessage).Name}";
    }

    public RedisStreamsConsumer(
        IHostEnvironment environment,
        IRedisConnectionProvider provider,
        ILogger<RedisStreamsConsumer<TMessage>> logger,
        IOptionsMonitor<RedisMessagingOptions> optionsMonitor)
    {
        _logger = logger;

        RedisMessagingOptions options = optionsMonitor.Get(MessagingOptionsDefaults.ConsumerName);

        if (options.BrokerConnectionName is null)
            throw new InvalidOperationException("Redis streams consumer has no broker connection");

        _redis = provider.GetRequiredConnection(options.BrokerConnectionName);

        string consumerGroup = options.ConsumerGroupName is null
            ? $"{environment.ApplicationName}.Consumer"
            : options.ConsumerGroupName;

        // We want to include Environment name as a form of Application 'isolation' to prevent
        // reading data from a different environment's Consumer Group although 'technically'
        // this should never be allowed if we create unique servers per environment.
        // e.g. "MyCompany.MyProduct.Consumer(Production)"
        _consumerGroup = $"{consumerGroup}({environment.EnvironmentName})";

        // Unique label for this server / IP as a consumer of a specific message type.
        _consumerName = $"{Environment.MachineName}-{typeof(TMessage).Name}";
    }

    /// <inheritdoc />
    public bool Subscribe(string stream)
    {
        // store the Stream name for future reference
        _streamName = stream;

        StreamGroupInfo[]? streamGroupInfo = null;

        // get any existing stream data from redis
        try
        {
            streamGroupInfo = Db.StreamGroupInfo(stream);
        }
        catch (RedisServerException)
        {
            // we don't care about this exception, StackExchange.Redis throws an exception when the streams are empty.
        }

        // check if the streams contains the consumer group we are expecting
        if (streamGroupInfo is not null && streamGroupInfo.Any(s => s.Name == _consumerGroup)) return true;

        // our consumer group didn't exist so lets create it
        if (Db.StreamCreateConsumerGroup(stream, _consumerGroup, StreamPosition.Beginning)) return true;

        // failed to create the group, log an error and return false
        _logger.LogError("Failed to subscribe to Stream {StreamName} for Consumer Group {ConsumerGroupName}",
            stream,
            _consumerGroup);

        return false;
    }

    /// <inheritdoc />
    public async Task<ICollection<MessageResult<TMessage>>?> ConsumeAsync(
        int? count = null,
        bool newMessagesOnly = true,
        CancellationToken ct = default)
    {
        StreamEntry[] entries = await Db.StreamReadGroupAsync(
            _streamName,
            _consumerGroup,
            _consumerName,
            newMessagesOnly
                ? StreamPosition.NewMessages
                : StreamPosition.Beginning,
            count);

        if (entries.Length is 0 || entries.All(se => se.IsNull)) return null;

        return entries
            .Where(entry => entry.IsNull is false)
            .Select(se => new MessageResult<TMessage>(se.Id, se.FromStreamEntry<TMessage>()))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<MessageResult<TMessage>?> ConsumeSingleAsync(bool newMessageOnly = true, CancellationToken ct = default)
    {
        StreamEntry[] entries = await Db.StreamReadGroupAsync(
            _streamName,
            _consumerGroup,
            _consumerName,
            newMessageOnly
                ? StreamPosition.NewMessages
                : StreamPosition.Beginning,
            1);

        if (entries.Length is 0 || entries.All(se => se.IsNull)) return null;

        if (entries.Length > 1)
        {
            _logger.LogWarning("Attempted to consume a single message off Stream {Name}, but received {Count}",
                _streamName,
                entries.Length);
        }

        return entries
            .Where(se => se.IsNull is false)
            .Select(entry => new MessageResult<TMessage>(entry.Id, entry.FromStreamEntry<TMessage>()))
            .SingleOrDefault();
    }

    /// <inheritdoc />
    public async Task<bool> CommitAsync(string messageId)
    {
        long messagesAcknowledged = await Db.StreamAcknowledgeAsync(_streamName, _consumerGroup, messageId);

        return messagesAcknowledged > 0;
    }
}
