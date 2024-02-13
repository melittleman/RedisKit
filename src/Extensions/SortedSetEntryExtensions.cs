using System.Collections.Generic;
using System.Linq;

namespace RedisKit.Extensions;

public static class SortedSetEntryExtensions
{   
    public static SortedSetEntry[] ToSortedSetEntries(this ICollection<KeyValuePair<RedisValue, double>> values)
    {
        List<SortedSetEntry> entries = new(values.Count);

        foreach (KeyValuePair<RedisValue, double> value in values)
        {
            // Implicit operator within SortedSetEntry
            // itself allow this conversion
            entries.Add(value);
        }

        return [.. entries];
    }

    public static RedisValue[] ToRedisValues<T>(this ICollection<T> sortedSets) where T : ISortedSetEntry
    {
        return sortedSets.ToSortedSetEntries()
            .Select(ss => ss.Element)
            .ToArray();
    }

    public static SortedSetEntry[] ToSortedSetEntries<T>(this ICollection<T> values) where T : ISortedSetEntry
    {
        ArgumentNullException.ThrowIfNull(values);

        return values
            .Select(value => new SortedSetEntry(value.Member, value.Score))
            .ToArray();
    }

    public static ICollection<KeyValuePair<RedisValue, double>> FromSortedSetEntries(this SortedSetEntry[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Length is 0) return Array.Empty<KeyValuePair<RedisValue, double>>();

        ICollection<KeyValuePair<RedisValue, double>> values = new Dictionary<RedisValue, double>();

        foreach (SortedSetEntry entry in entries)
        {
            // Implicit operator within SortedSetEntry
            // itself allow this conversion
            values.Add(entry);
        }

        return values;
    }

    public static ICollection<T> FromSortedSetEntries<T>(this SortedSetEntry[] entries) where T : ISortedSetEntry
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Length is 0) return Array.Empty<T>();

        List<T> values = [];

        foreach (SortedSetEntry sortedSet in entries)
        {
            T? value = (T?)Activator.CreateInstance(typeof(T));

            if (value is null) continue;

            value.Member = sortedSet.Element.ToString();
            value.Score = sortedSet.Score;

            values.Add(value);
        }

        return values;
    }
}
