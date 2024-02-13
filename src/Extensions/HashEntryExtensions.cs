using System.Linq;
using System.Reflection;

namespace RedisKit.Extensions;

public static class HashEntryExtensions
{
    public static HashEntry[] ToHashEntries<T>(this T value)
    {
        ArgumentNullException.ThrowIfNull(value);

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
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Length is 0) return default;

        PropertyInfo[] properties = typeof(T).GetProperties();

        T? value = (T?)Activator.CreateInstance(typeof(T));

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
