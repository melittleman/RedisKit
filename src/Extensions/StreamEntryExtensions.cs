using System.Linq;

namespace NRedisKit.Extensions;

public static class StreamEntryExtensions
{
    public static T? FromStreamEntry<T>(this StreamEntry stream)
    {
        if (stream.IsNull) return default;
        if (stream.Values.Any() is false) return default;

        return stream.Values.FromNameValueEntries<T>();
    }
}
