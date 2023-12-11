using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NRedisKit;

/// <summary>
///     Reprents a wrapper around a named <see cref="RedisContext" /> that provides
///     helpful methods for storing and retrieving data from the Redis server.
/// </summary>
public sealed partial record RedisClient
{
    private readonly RedisJsonOptions _jsonOptions;
    private readonly ILogger<RedisClient> _logger;
    private readonly IRedisContext _redis;

    public IDatabase Db => _redis.Db;

    public ISubscriber Subscriber => _redis.Subscriber;

    internal RedisClient(
        ILoggerFactory loggers,
        IRedisContext redis,
        RedisJsonOptions jsonOptions)
    {
        if (loggers is null) throw new ArgumentNullException(nameof(loggers));

        _redis = redis ?? throw new ArgumentNullException(nameof(redis));

        _logger = loggers.CreateLogger<RedisClient>();
        _jsonOptions = jsonOptions;
    }

    /// <summary>
    ///     Deletes all values at given <paramref name="keys"/>.
    /// </summary>
    /// <param name="keys">The keys to delete values of.</param>
    /// <returns>
    ///     <see langword="true"/> when all <paramref name="keys"/> were deleted
    ///     or <see langword="false"/> when at least one key did not exist.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="keys"/> is null.
    /// </exception>
    public async Task<bool> DeleteAllAsync(IReadOnlyCollection<string> keys)
    {
        if (keys is null) throw new ArgumentNullException(nameof(keys));

        _logger.LogTrace("Deleting all values at {Keys}", string.Join(", ", keys));

        long deletedCount = await Db.KeyDeleteAsync(keys.Select(k => new RedisKey(k)).ToArray());
        var requestedCount = keys.Count();

        _logger.LogDebug("Deleted {DeletedCount} keys of {RequestedCount} requested.", deletedCount, requestedCount);

        return deletedCount == requestedCount;
    }

    #region Strings

    /// <summary>
    ///     Set the <see cref="string"/>value at the given <see cref="string"/> key.
    /// </summary>
    /// <param name="key">
    ///     The <see cref="string"/> cache key to set.
    /// </param>
    /// <param name="value">
    ///     The <see cref="string"/> cache value to set.
    /// </param>
    /// <param name="expiry">
    ///     An optional expiry <see cref="TimeSpan"/> that sets
    ///     the cache key TTL (Time To Live) when provided.
    /// </param>
    /// <remarks>
    ///     Will overwrite if it already exists.
    /// </remarks>
    /// <returns>
    ///     <see langword="true" /> if the string was set, otherwise <see langword="false" />.
    /// </returns>
    public Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (value is null) throw new ArgumentNullException(nameof(value));

        return Db.StringSetAsync(key, value, expiry);
    }

    /// <summary>
    ///     Get the <see cref="string"/> value at the given <see cref="string"/> key.
    /// </summary>
    /// <param name="key">
    ///     The <see cref="string"/> cache key to retrieve.
    /// </param>
    /// <returns>
    ///     The <see cref="string"/> value of the cache key when it exists.
    ///     Otherwise null when it does not.
    /// </returns>
    public async Task<string?> GetStringAsync(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        _logger.LogTrace("Getting string value from {Key}", key);

        try
        {
            return await Db.StringGetAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to get string from {Key}", key);
            return null;
        }
    }

    #endregion

    #region Lists

    // TODO: Methods below are really List operations so should be renamed
    // to better reflect how they are storing the data within Redis

    public async Task<string?[]?> GetAllStringsAsync(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        _logger.LogTrace("Getting all strings at {Key}", key);

        RedisValue[] values = await Db.ListRangeAsync(key);

        return values?.ToStringArray();
    }

    public async Task<string?[]?> AddToStringsAsync(string key, params string[] values)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (values is null) throw new ArgumentNullException(nameof(values));

        _logger.LogTrace("Adding {Values} to all strings at {Key}", string.Join(", ", values), key);

        long result = await Db.ListRightPushAsync(key, values.ToRedisValueArray());

        if (result is 0) return [];

        // Return the newly updated list
        return await GetAllStringsAsync(key);
    }

    public async Task<string?[]?> RemoveFromStringsAsync(string key, string value)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (value is null) throw new ArgumentNullException(nameof(value));

        _logger.LogTrace("Removing {Value} from strings at {Key}", value, key);

        long result = await Db.ListRemoveAsync(key, value);

        if (result is 0)
        {
            // We failed to remove the requested string from the collection.
            // Not neccessarily a bad thing, maybe it wasn't there in the first place?
            // I think just a log of warning is enough here, and we can still
            // return the string collection, rather than a null or empty value.
            _logger.LogWarning("Failed to remove string {Value} at {Key}", value, key);
        }

        // Return the updated list
        return await GetAllStringsAsync(key);
    }

    #endregion

    #region Sets

    public async Task<bool> AddToSetAsync(string key, string value, TimeSpan? expiry = null)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (value is null) throw new ArgumentNullException(nameof(value));

        _logger.LogTrace("Adding {Value} to Set at {Key}", value, key);

        if (await Db.SetAddAsync(key, value) is false)
        {
            _logger.LogInformation("The member {Value} was already present in Set at {Key}", value, key);

            // Returning true here as this indicates that the value was already
            // present in the Set. Technically we have achieved what was requested!
            // TODO: We may want to still try set the TTL if the key already exists?
            return true;
        }

        if (expiry is null) return true;

        if (await Db.KeyExpireAsync(key, (TimeSpan)expiry) is false)
        {
            _logger.LogWarning("Failed to update expiry TTL on Set at {Key}", key);
        }

        // This should still be okay to return true seeing as the value was successfully added
        // to the Set. At least with the warning log we can identify any issues that may arise.
        return true;
    }

    public Task<bool> RemoveFromSetAsync(string key, string value)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (value is null) throw new ArgumentNullException(nameof(value));

        _logger.LogTrace("Removing {Value} from Set at {Key}", value, key);

        return Db.SetRemoveAsync(key, value);
    }

    #endregion

    #region Sorted Sets

    public Task<double?> GetScoreAsync(string key, string member)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (member is null) throw new ArgumentNullException(nameof(member));

        _logger.LogTrace("Getting score for {Member} at {Key}", member, key);

        return Db.SortedSetScoreAsync(key, member);
    }

    // TODO: Prefer 'AddToSortedSetAsync' as the method name here. It will add to it if it exists already so isn't
    // truly a 'Set' command. We could probably decorate with [Obselete] if we are worried about a breaking change.
    public async Task<ICollection<T>> SetAsSortedSetAsync<T>(string key, ICollection<T> values) where T : ISortedSetEntry
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (values is null) throw new ArgumentNullException(nameof(values));

        _logger.LogTrace("Setting {Count} Sorted Set members at {Key}", values.Count, key);

        SortedSetEntry[] sets = values.ToSortedSetEntries();
        if (sets is null) return Array.Empty<T>();

        long result = await Db.SortedSetAddAsync(key, sets);

        if (result <= 0)
        {
            // Not neccessarily a problem as Redis won't include
            // Sorted Sets that already existed and have had their 
            // Score updated in this results count.
            _logger.LogDebug("No sorted set entries were add to {Key}", key);
        }

        // TODO: Need to unify the return logic between Hash, JSON and Sorted Set
        // as we can either use true/false for success, return the same parameter that
        // was passed in, or re-read the key and return what was actually stored?
        return values;
    }

    public async Task<bool> RemoveFromSortedSetAsync(string key, string member)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (member is null) throw new ArgumentNullException(nameof(member));

        _logger.LogTrace("Removing member {Member} from Sorted Set at {Key}", member, key);

        // Redis will return false when the member does not already exist in the Sorted Set.
        // So we don't really want to use this as the return, providing it doesn't crash
        // we have to assume that the delete was successful.
        if (await Db.SortedSetRemoveAsync(key, member) is false)
        {
            _logger.LogDebug("Sorted Set entry {Member} at {Key} already did not exist", member, key);
        }

        return true;
    }

    public async Task<bool> RemoveFromSortedSetAsync<T>(string key, params T[] sets) where T : ISortedSetEntry
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (sets is null) throw new ArgumentNullException(nameof(sets));

        _logger.LogTrace("Removing {Count} members from Sorted Set at {Key}", sets.Count(), key);

        long removedCount = await Db.SortedSetRemoveAsync(key, sets.ToRedisValues());

        // This is not neccessarily an issue as the count will
        // be 0 if the requested member for removal, was not in
        // the Sorted Set to begin with.
        if (removedCount is 0)
        {
            _logger.LogDebug("Sorted Set members to be removed from {Key} already did not exist", key);
        }

        // Providing we didn't crash, I think it's safe to
        // assume this was successful and return true here.
        return true;
    }

    // Private method here as the Sorted Sets data type is not likely to be interacted with directly by a client.
    // Instead this serves as a joining table between different keys within the Redis server that may then be stored as Hashes etc.
    private async Task<ICollection<T?>> GetAllHashesFromSortedSetKeysAsync<T>(IReadOnlyCollection<SortedSetEntry> sets) where T : ISortedSetEntry, IHashEntry
    {
        if (sets is null) throw new ArgumentNullException(nameof(sets));

        _logger.LogTrace("Getting from Hashes at {Count} Sorted Set keys", sets.Count);

        IReadOnlyCollection<string> keys = sets
            .Select(s => s.Element.ToString())
            .ToList()
            .AsReadOnly();

        ICollection<T?> hashes = await GetAllFromHashesAsync<T>(keys);

        // Updates the 'Score' property for the retrieved Hash with what
        // Sorted Set score was stored with it's key. Allows for using a
        // Hash in multiple joining relationships with different 'Scores'.
        return hashes.Select(hash =>
        {
            if (hash is null) return default;

            SortedSetEntry set = sets.SingleOrDefault(s => s.Element.ToString().Equals(hash.Key));

            hash.Score = set.Score;

            return hash;

        }).ToList();
    }

    #endregion
}
