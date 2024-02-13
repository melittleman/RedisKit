namespace RedisKit.Extensions;

public static class StreamEntryExtensions
{
    public static T? FromStreamEntry<T>(this StreamEntry stream)
    {
        if (stream.IsNull) return default;
        if (stream.Values.Length is 0) return default;

        return stream.Values.FromNameValueEntries<T>();
    }
}
