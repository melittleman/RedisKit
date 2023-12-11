using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace NRedisKit;

/// <inheritdoc />
public sealed partial record RedisClient
{
    public async Task<T?> GetFromHashAsync<T>(string key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        _logger.LogTrace("Getting Hash value at {Key}", key);

        HashEntry[] entries = await Db.HashGetAllAsync(key);

        return entries.FromHashEntries<T>();
    }

    public async Task<ICollection<T?>> GetAllFromHashesAsync<T>(IReadOnlyCollection<string> keys)
    {
        if (keys is null) throw new ArgumentNullException(nameof(keys));

        _logger.LogTrace("Getting Hash values at {Keys}", string.Join(", ", keys));

        // A Redis 'Batch' is similar to a 'Transaction' in that they both use Pipelining to ensure
        // requests are sent through as one unit, thus reducing the possibility of unoptimized packet sizes.
        // However a Batch is better for read scenarios as it is non-blocking on the server,
        // whereas a Transaction will block all other clients until the operation has completed,
        // making it more useful for writes where all requests need to execute atomically.
        IBatch batch = Db.CreateBatch();

        // TODO: If you await a transaction command, the connection 'hangs'. We should implement this pattern
        // lower down so it can be reused. https://stackoverflow.com/questions/25976231/stackexchange-redis-transaction-methods-freezes
        Task<HashEntry[]>[] batchTasks = keys.Select(key => batch.HashGetAllAsync(key)).ToArray();

        batch.Execute();

        HashEntry[][] completedTasks = await Task.WhenAll(batchTasks);

        return
        (
            from entries in completedTasks
            where entries?.Any() is true
            select entries.FromHashEntries<T>()

        ).ToList();
    }

    public async Task<ICollection<T?>> GetAllFromHashesAsync<T>(string key) where T : ISortedSetEntry, IHashEntry
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        _logger.LogTrace("Getting all Hash keys from {Key}", key);

        // TODO: There are a lot of different ways to range the Sorted Set by...
        // We should investigate further to decide on what the 'best' approach is.
        SortedSetEntry[] entries = await Db.SortedSetRangeByRankWithScoresAsync(key);

        if (entries is null) return Array.Empty<T>();

        return await GetAllHashesFromSortedSetKeysAsync<T>(entries);
    }

    public async Task<T?> SetAsHashAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (value is null) throw new ArgumentNullException(nameof(value));

        _logger.LogTrace("Setting Hash {@Value} at {Key}", value, key);

        HashEntry[] hash = value.ToHashEntries();
        if (hash is null) return default;

        // TODO: Would be great if this actually returned an indication of success...
        // Should we be wrapping try...catch around this to check for failure?
        await Db.HashSetAsync(key, hash);

        if (expiry is null) return value;

        if (await Db.KeyExpireAsync(key, (TimeSpan)expiry) is false)
        {
            // The JSON document was set successfull, but we failed to 
            // save the TTL expiry on the key, this is probably still ok
            // to return true, with a log of the warning message.
            _logger.LogWarning("Failed to set the Hash value TTL at {Key}", key);
        }

        return value;
    }

    // TODO: Need to introduce the ability to expire each Hash, maybe even with a different value?
    public async Task<ICollection<T?>> SetAllAsHashesAsync<T>(ICollection<T> values) where T : IHashEntry
    {
        if (values is null) throw new ArgumentNullException(nameof(values));

        _logger.LogTrace("Setting {Count} Hash values at individual keys", values.Count);

        ITransaction transaction = Db.CreateTransaction();

        // TODO: If you await a transaction command, the connection 'hangs'. We should implement this pattern
        // lower down so it can be reused. https://stackoverflow.com/questions/25976231/stackexchange-redis-transaction-methods-freezes
        Task[] hashSetTasks = values.Select(value =>
        {
            HashEntry[] hash = value.ToHashEntries();

            if (hash is not null)
            {
                return transaction.HashSetAsync(value.Key, hash);
            }

            return Task.CompletedTask;

        }).ToArray();

        if (await transaction.ExecuteAsync())
        {
            // TODO: Not 100% convinced we actually need to await all Tasks here
            // but in theory they 'should' all now been marked as Completed.
            await Task.WhenAll(hashSetTasks);

            // Null-forgiving here as we know "values" is not null
            return values!;
        }
        else
        {
            _logger.LogError("Failed to execute the transaction to set {Count} Hashes", values.Count);
        }

        return Array.Empty<T>();
    }

    public async Task<ICollection<T?>> SetAllAsHashesAsync<T>(string key, ICollection<T> values) where T : IHashEntry, ISortedSetEntry
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (values is null) throw new ArgumentNullException(nameof(values));

        _logger.LogTrace("Setting {Count} Hash values at {Key}", values.Count, key);

        ITransaction transaction = Db.CreateTransaction();

        SortedSetEntry[] sets = values.ToSortedSetEntries();
        if (sets is null) return Array.Empty<T>();

        Task<long> sortedSetTask = transaction.SortedSetAddAsync(key, sets);

        // TODO: This can probably be unified with the above method to reduce duplication
        Task[] hashSetTasks = values.Select(value =>
        {
            HashEntry[] hash = value.ToHashEntries();

            if (hash is not null)
            {
                return transaction.HashSetAsync(value.Key, hash);
            }

            return Task.CompletedTask;

        }).ToArray();

        if (await transaction.ExecuteAsync())
        {
            // TODO: Not 100% convinced we actually need to await all Tasks here
            // but in theory they 'should' all now been marked as Completed.
            await Task.WhenAll(hashSetTasks);
            long setsAdded = await sortedSetTask;

            if (setsAdded <= 0)
            {
                // Not neccessarily a problem as Redis won't include
                // Sorted Sets that already existed and have had their 
                // Score updated in this results count.
                _logger.LogDebug("No sorted set entries were add to {Key}", key);
            }

            // Null-forgiving here as we know "values" is not null
            return values!;
        }

        return Array.Empty<T>();
    }

    public async Task<T?> GetOrSetAsHashAsync<T>(string key, Func<Task<T?>> factory) where T : IHashEntry
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (factory is null) throw new ArgumentNullException(nameof(factory));

        _logger.LogTrace("Getting or setting Hash from {Key}", key);

        T? result = await GetFromHashAsync<T>(key);
        if (result is not null) return result;

        T? value = await factory();
        if (value is null) return default;

        // TODO: Probably need to add another overload / optional param
        // for passing in the expiry TimeSpan?
        return await SetAsHashAsync(key, value);
    }

    public async Task<ICollection<T?>> GetOrSetAllAsHashesAsync<T>(
        string key,
        Func<Task<ICollection<T>?>> factory) where T : ISortedSetEntry, IHashEntry
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (factory is null) throw new ArgumentNullException(nameof(factory));

        _logger.LogTrace("Getting all Hashes from {Key}", key);

        ICollection<T?> results = await GetAllFromHashesAsync<T>(key);

        // TODO: Did we find them all?
        if (results is not null && results.Any()) return results;

        ICollection<T>? values = await factory();
        if (values is null) return Array.Empty<T>();

        return await SetAllAsHashesAsync(key, values);
    }

    public async Task<ICollection<T?>> GetOrSetAllAsHashesAsync<T>(
        IReadOnlyCollection<string> keys,
        Func<Task<ICollection<T>>> factory) where T : IHashEntry
    {
        if (keys is null) throw new ArgumentNullException(nameof(keys));
        if (factory is null) throw new ArgumentNullException(nameof(factory));

        _logger.LogTrace("Getting Hashes at {Count} keys", keys.Count);

        ICollection<T?> results = await GetAllFromHashesAsync<T>(keys);

        // Did we find them all?
        if (results is not null && results.Count == keys.Count) return results;

        ICollection<T> values = await factory();

        return await SetAllAsHashesAsync(values);
    }

    public async Task<ICollection<T?>> SearchOrSetAllAsHashesAsync<T>(
        string indexName,
        string searchTerm,
        Func<Task<ICollection<T>>> factory) where T : IHashEntry
    {
        if (searchTerm is null) throw new ArgumentNullException(nameof(searchTerm));
        if (factory is null) throw new ArgumentNullException(nameof(factory));

        _logger.LogTrace("Searching all Hashes with query {Term}", searchTerm);

        ICollection<T> results = await SearchAsync<T>(indexName, GetDefaultQuery(searchTerm));

        // TODO: We found some, but did we find ALL??
        if (results is not null && results.Any()) return results!;

        ICollection<T> values = await factory();

        return await SetAllAsHashesAsync(values);
    }

    public async Task<T?> SearchSingleOrSetAsHashAsync<T>(
        string indexName,
        string searchTerm,
        Func<Task<T>> factory) where T : IHashEntry
    {
        if (searchTerm is null) throw new ArgumentNullException(nameof(searchTerm));
        if (factory is null) throw new ArgumentNullException(nameof(factory));

        _logger.LogTrace("Searching for single Hash with query {Term}", searchTerm);

        T? result = await SearchSingleAsync<T>(indexName, searchTerm);
        if (result is not null) return result;

        T value = await factory();

        return await SetAsHashAsync<T>(value.Key, value);
    }
}
