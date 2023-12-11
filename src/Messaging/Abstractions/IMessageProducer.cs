using System.Threading.Tasks;

namespace NRedisKit.Messaging.Abstractions;

/// <summary>
///     Message producer abstraction.
/// </summary>
/// <typeparam name="TMessage">
///     The type of Message to produce.
/// </typeparam>
public interface IMessageProducer<in TMessage> where TMessage : class
{
    /// <summary>
    ///     Sends a message to the queue.
    /// </summary>
    /// <param name="queueName">The queue name to send this message to.</param>
    /// <param name="message">The Message to send.</param>
    /// <returns>
    ///     A boolean that indicated whether or not the send was successful.
    /// </returns>
    Task<bool> SendAsync(string queueName, TMessage message);
}
