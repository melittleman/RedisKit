using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace RedisKit.Messaging.Abstractions;

/// <summary>
///     Interface for implementations of Message Consumers.
/// </summary>
/// <typeparam name="TMessage">
///     Message type to consume.
/// </typeparam>
public interface IMessageConsumer<TMessage>
{
    /// <summary>
    ///     Subscribe the consumer to a message queue.
    /// </summary>
    /// 
    /// <remarks>
    ///     A consumer must subscribe to a queue before it can start receiving messages.
    /// </remarks>
    /// 
    /// <param name="queue">
    ///     Name of the message queue to subscribe to.
    /// </param>
    bool Subscribe(string queue);

    /// <summary>
    ///     Attempts to consume messages from the queue.
    /// </summary>
    /// 
    /// <param name="count">Number of messages to consume.</param>
    /// <param name="newMessagesOnly">Only return new messages to the Consumer.</param>
    /// <param name="ct">A cancellation token</param>
    /// 
    /// <returns>
    ///     A collection of consumed <typeparamref name="TMessage"/> results when successful.
    ///     Otherwise null.
    /// </returns>
    Task<ICollection<MessageResult<TMessage>>?> ConsumeAsync(
        int? count = null,
        bool newMessagesOnly = true,
        CancellationToken ct = default);

    /// <summary>
    ///     Attempts to consume a single message from the queue.
    /// </summary>
    /// 
    /// <param name="newMessageOnly">Whether to only read a new message.</param>
    /// <param name="ct">A cancellation token.</param>
    /// 
    /// <returns>
    ///     A single <see cref="MessageResult{TMessage}"/> instance when successful,
    ///     otherwise null.
    /// </returns>
    Task<MessageResult<TMessage>?> ConsumeSingleAsync(
        bool newMessageOnly = true,
        CancellationToken ct = default);

    /// <summary>
    ///     Signal to broker that the message consumer has read and processed the message.
    /// </summary>
    /// 
    /// <remarks>
    ///     By telling the broker the message has been consumed it won't be presented again to consumers.
    /// </remarks>
    /// 
    /// <param name="messageId">The message ID to commit</param>
    Task<bool> CommitAsync(string messageId);
}
