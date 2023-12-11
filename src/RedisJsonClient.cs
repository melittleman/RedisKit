using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using NRedisStack;
using NRedisStack.RedisStackCommands;

namespace NRedisKit;

/// <inheritdoc />
public sealed partial record RedisClient
{
    public JsonCommands Json => Db.JSON();

    //private readonly JsonSerializerOptions SerializerOptions = new()
    //{
    //    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    //    ReferenceHandler = ReferenceHandler.IgnoreCycles,
    //    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    //    PropertyNameCaseInsensitive = true,
    //    Converters =
    //    {
    //        // TODO: Implement a 'proper' way to retrieve these dynamically
    //        // from DI as the user may also have provided custom converters.

    //        new JsonStringEnumConverter(),
    //        new AuthenticationTicketJsonConverter()
    //    }
    //};

    public Task<T?> GetFromJsonAsync<T>(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        _logger.LogTrace("Getting JSON value at {Key}", key);        

        return Json.GetAsync<T>(key, serializerOptions: _jsonOptions.Serializer);
    }

    public async Task<ICollection<T?>> GetFromJsonArrayAsync<T>(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        _logger.LogTrace("Getting JSON array from {Key}", key);

        IEnumerable<T?> result = await Json.GetEnumerableAsync<T>(key);

        return result.ToList();
    }

    public async Task<bool> SetAsJsonAsync<T>(
        string key,
        T document,
        string jsonPath = "$",
        TimeSpan? expiry = null)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (document is null) throw new ArgumentNullException(nameof(document));

        _logger.LogTrace("Setting JSON value {@Document} at {Key}", document, key);

        // TODO: This doesn't accept JsonSerializerOptions?
        bool success = await Json.SetAsync(key, jsonPath, document);

        if (success is false)
        {
            _logger.LogError("Failed to set JSON value at {Key}", key);
            return false;
        }

        if (expiry is null) return success;

        if (await Db.KeyExpireAsync(key, (TimeSpan)expiry) is false)
        {
            // The JSON document was set successfull, but we failed to 
            // save the TTL expiry on the key, this is probably still ok
            // to return true, with a log of the warning message.
            _logger.LogWarning("Failed to set the JSON value TTL at {Key}", key);
        }

        return true;
    }
}
