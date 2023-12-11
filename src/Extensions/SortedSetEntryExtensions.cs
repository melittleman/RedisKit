using System.Collections.Generic;
using System.Linq;

namespace NRedisKit.Extensions;

public static class SortedSetEntryExtensions
{   
    public static SortedSetEntry[] ToSortedSetEntries(this ICollection<KeyValuePair<RedisValue, double>> values)
    {
       IList<SortedSetEntry> entries = new List<SortedSetEntry>(values.Count());

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
        if (values is null) throw new ArgumentNullException(nameof(values));

        return values
            .Select(value => new SortedSetEntry(value.Member, value.Score))
            .ToArray();
    }

    public static ICollection<KeyValuePair<RedisValue, double>> FromSortedSetEntries(this SortedSetEntry[] entries)
    {
        if (entries is null) throw new ArgumentNullException(nameof(entries));
        if (entries.Any() is false) throw new ArgumentException("Does not contain any elements", nameof(entries));

        ICollection<KeyValuePair<RedisValue, double>> values = new Dictionary<RedisValue, double>();

        foreach (SortedSetEntry entry in entries)
        {
            // Implicit operator within SortedSetEntry
            // itself allow this conversion
            values.Add(entry);
        }

        return values;
    }

    public static ICollection<T>? FromSortedSetEntries<T>(this SortedSetEntry[] entries) where T : ISortedSetEntry
    {
        if (entries is null) throw new ArgumentNullException(nameof(entries));
        if (entries.Any() is false) return null;

        List<T> values = [];

        foreach (SortedSetEntry sortedSet in entries)
        {
            T value = (T)Activator.CreateInstance(typeof(T));

            if (value is null) continue;

            value.Member = sortedSet.Element.ToString();
            value.Score = sortedSet.Score;

            values.Add(value);
        }

        return values;
    }
}
