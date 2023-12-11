using System.Collections.Generic;
using System.Linq;

using NRedisStack.Search;

namespace NRedisKit.Extensions;

public static class DocumentExtensions
{
    public static T? FromSearchDocument<T>(this Document document)
    {
        IEnumerable<KeyValuePair<string, RedisValue>> props = document.GetProperties();

        // TODO: Is there an easier way to do this that the NRedisStack library already supports?
        HashEntry[] entries = props
            .Select(p => new HashEntry(p.Key, p.Value))
            .ToArray();

        return entries.FromHashEntries<T>();
    }
}
