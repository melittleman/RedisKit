using System.Linq;
using System.Reflection;

namespace NRedisKit.Extensions;

public static class HashEntryExtensions
{
    public static HashEntry[] ToHashEntries<T>(this T value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));

        PropertyInfo[] properties = value
            .GetType()
            .GetProperties();

        return properties
            .Where(property => property.GetValue(value) is not null && property.CanRead is true)
            .Select(property => new HashEntry(
                property.Name,
                property.GetValue(value).GetRedisValue())
            ).ToArray();
    }

    public static T? FromHashEntries<T>(this HashEntry[] entries)
    {
        if (entries is null) throw new ArgumentNullException(nameof(entries));
        if (entries.Any() is false) return default;

        PropertyInfo[] properties = typeof(T).GetProperties();

        T value = (T)Activator.CreateInstance(typeof(T));

        foreach (PropertyInfo property in properties)
        {
            HashEntry entry = entries.FirstOrDefault(hash => hash.Name.ToString().Equals(property.Name));

            if (property.CanWrite is false ||
                entry == default ||
                entry.Equals(new HashEntry())) continue;

            property.SetValue(value, entry.Value.GetProperty(property.PropertyType));
        }

        return value;
    }
}
