namespace NRedisKit.Messaging;

/// <summary>
///     Represents the result of a single message consumption from the broker.
/// </summary>
/// 
/// <typeparam name="TMessage">The type of message consumed.</typeparam>
public record MessageResult<TMessage>(string? Key, TMessage? Message)
{
    /// <summary>
    ///     Message key or identifier
    /// </summary>
    public string? Key { get; set; } = Key;

    /// <summary>
    ///     The message's value
    /// </summary>
    public TMessage? Message { get; set; } = Message;
}
