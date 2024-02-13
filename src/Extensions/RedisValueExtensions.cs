using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace RedisKit.Extensions;

internal static class RedisValueExtensions
{
    internal static RedisValue GetRedisValue(this object? property)
    {
        if (property is null) return RedisValue.Null;

        return property switch
        {
            IEnumerable<object> => JsonSerializer.Serialize(property),

            DateTime time => time.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture), // ISO 8601 w/ UTC Time-Zone

            _ => Convert.ToString(property, CultureInfo.InvariantCulture) ?? RedisValue.EmptyString
        };
    }

    internal static object? GetProperty(this RedisValue value, Type propertyType)
    {
        ArgumentNullException.ThrowIfNull(propertyType);

        if (value.IsNull) return null;

        return Convert.ToString(value, CultureInfo.InvariantCulture)?.ChangeType(propertyType);
    }
}
