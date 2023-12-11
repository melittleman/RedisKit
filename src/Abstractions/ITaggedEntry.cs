namespace NRedisKit.Abstractions;

public interface ITaggedEntry
{
    /// <summary>
    ///     A tag field can be used to group related entries within a RediSearch index.
    /// </summary>
    /// <remarks>
    ///     See: https://redis.io/docs/stack/search/reference/tags
    /// </remarks>
    string Tags { get; set; }
}
