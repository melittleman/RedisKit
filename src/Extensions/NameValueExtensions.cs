using System.Linq;
using System.Reflection;

namespace RedisKit.Extensions;

public static class NameValueExtensions
{
    public static NameValueEntry[] ToNameValueEntries<T>(this T value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));

        PropertyInfo[] properties = value
            .GetType()
            .GetProperties();

        return properties
            .Where(property => property.GetValue(value) is not null && property.CanRead)
            .Select(property => new NameValueEntry(
                property.Name,
                property.GetValue(value)?.GetRedisValue() ?? string.Empty)
            ).ToArray();
    }

    public static T? FromNameValueEntries<T>(this NameValueEntry[] entries)
    {
        if (entries is null) throw new ArgumentNullException(nameof(entries));
        if (entries.Any() is false) return default;

        PropertyInfo[] properties = typeof(T).GetProperties();

        T? value = (T?)Activator.CreateInstance(typeof(T));

        foreach (PropertyInfo property in properties)
        {
            NameValueEntry entry = entries.FirstOrDefault(nve => nve.Name.ToString().Equals(property.Name));

            if (property.CanWrite is false || entry == default || entry.Equals(new NameValueEntry())) continue;

            property.SetValue(value,entry.Value.GetProperty(property.PropertyType));
        }

        return value;
    }
}
