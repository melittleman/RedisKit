using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RedisKit.Extensions;

public static class DatabaseExtensions
{
    #region Hashes

    public static async Task<T?> HashGetAsync<T>(this IDatabase db, RedisKey key)
    {
        ArgumentNullException.ThrowIfNull(db);

        HashEntry[] entries = await db.HashGetAllAsync(key);

        return entries.FromHashEntries<T>();
    }

    public static async Task<ICollection<T?>> HashGetAllAsync<T>(this IDatabase db, IReadOnlyCollection<RedisKey> keys)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(keys);

        // A Redis 'Batch' is similar to a 'Transaction' in that they both use Pipelining to ensure
        // requests are sent through as one unit, thus reducing the possibility of unoptimized packet sizes.
        // However a Batch is better for read scenarios as it is non-blocking on the server,
        // whereas a Transaction will block all other clients until the operation has completed,
        // making it more useful for writes where all requests need to execute atomically.
        IBatch batch = db.CreateBatch();

        // TODO: If you await a transaction command, the connection 'hangs'. We should implement this pattern
        // lower down so it can be reused. https://stackoverflow.com/questions/25976231/stackexchange-redis-transaction-methods-freezes
        Task<HashEntry[]>[] batchTasks = keys.Select(key => batch.HashGetAllAsync(key)).ToArray();

        batch.Execute();

        HashEntry[][] completedTasks = await Task.WhenAll(batchTasks);

        return
        (
            from entries in completedTasks
            where entries?.Length > 0
            select entries.FromHashEntries<T>()

        ).ToList();
    }

    public static async Task<ICollection<T?>> HashGetAllFromSortedSetAsync<T>(this IDatabase db, RedisKey key) where T : ISortedSetEntry, IHashEntry
    {
        ArgumentNullException.ThrowIfNull(db);

        // TODO: There are a lot of different ways to range the Sorted Set by...
        // We should investigate further to decide on what the 'best' approach is.
        SortedSetEntry[] entries = await db.SortedSetRangeByRankWithScoresAsync(key);

        if (entries is null) return Array.Empty<T>();

        return await GetAllHashesFromSortedSetsAsync<T>(db, entries);
    }

    public static async Task<bool> HashSetAndExpireAsync<T>(this IDatabase db, RedisKey key, T value, TimeSpan? expiry = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(value);

        HashEntry[] hash = value.ToHashEntries();
        if (hash is null) return default;

        // TODO: Would be great if this actually returned an indication of success...
        // Should we be wrapping try...catch around this to check for failure?
        await db.HashSetAsync(key, hash);

        if (expiry is null) return true;

        // TODO: What about if the Set succeeds but the Expire fails?
        return await db.KeyExpireAsync(key, (TimeSpan)expiry);
    }

    // TODO: Need to introduce the ability to expire each Hash, maybe even with a different value?
    public static async Task<ICollection<T?>> HashSetAllAsync<T>(this IDatabase db, ICollection<T> values) where T : IHashEntry
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(values);

        ITransaction transaction = db.CreateTransaction();

        Task[] hashSetTasks = AddHashSetsToTransaction(transaction, values);

        if (await transaction.ExecuteAsync())
        {
            // TODO: Not 100% convinced we actually need to await all Tasks here
            // but in theory they 'should' all now been marked as Completed.
            await Task.WhenAll(hashSetTasks);

            // Null-forgiving here as we know "values" is not null
            return values!;
        }

        return Array.Empty<T>();
    }

    public static async Task<ICollection<T?>> HashSetAllAsSortedSetAsync<T>(
        this IDatabase db,
        RedisKey key,
        ICollection<T> values) where T : IHashEntry, ISortedSetEntry
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(values);

        ITransaction transaction = db.CreateTransaction();

        SortedSetEntry[] sets = values.ToSortedSetEntries();
        if (sets is null) return Array.Empty<T>();

        Task<long> sortedSetTask = transaction.SortedSetAddAsync(key, sets);

        Task[] hashSetTasks = AddHashSetsToTransaction(transaction, values);

        if (await transaction.ExecuteAsync())
        {
            // TODO: Not 100% convinced we actually need to await all Tasks here
            // but in theory they 'should' all now been marked as Completed.
            await Task.WhenAll(hashSetTasks);
            long _ = await sortedSetTask;

            // TODO: What should we do when 'setsAdded' is 0?

            // Null-forgiving here as we know "values" is not null
            return values!;
        }

        return Array.Empty<T>();
    }

    public static async Task<T?> HashGetOrSetAsync<T>(this IDatabase db, RedisKey key, Func<Task<T?>> factory) where T : IHashEntry
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(factory);

        T? result = await HashGetAsync<T>(db, key);
        if (result is not null) return result;

        T? value = await factory();
        if (value is null) return default;

        // TODO: Probably need to add another overload / optional param
        // for passing in the expiry TimeSpan?
        return await HashSetAndExpireAsync(db, key, value) ? value : default;
    }

    public static async Task<ICollection<T?>> HashGetOrSetAllAsSortedSetAsync<T>(
        this IDatabase db, 
        RedisKey key,
        Func<Task<ICollection<T>?>> factory) where T : ISortedSetEntry, IHashEntry
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(factory);

        ICollection<T?> results = await HashGetAllFromSortedSetAsync<T>(db, key);

        // TODO: Did we find them all?
        if (results is not null && results.Count > 0) return results;

        ICollection<T>? values = await factory();
        if (values is null) return Array.Empty<T>();

        return await HashSetAllAsSortedSetAsync(db, key, values);
    }

    public static async Task<ICollection<T?>> HashGetOrSetAllAsync<T>(
        this IDatabase db,
        IReadOnlyCollection<RedisKey> keys,
        Func<Task<ICollection<T>>> factory) where T : IHashEntry
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(factory);

        ICollection<T?> results = await HashGetAllAsync<T>(db, keys);

        // Did we find them all?
        if (results is not null && results.Count == keys.Count) return results;

        ICollection<T> values = await factory();

        return await HashSetAllAsync(db, values);
    }

    // Private method here as the Sorted Sets data type is not likely to be interacted with directly by a client.
    // Instead this serves as a joining table between different keys within the Redis server that may then be stored as Hashes etc.
    private static async Task<ICollection<T?>> GetAllHashesFromSortedSetsAsync<T>(
        IDatabase db,
        IReadOnlyCollection<SortedSetEntry> sets) where T : ISortedSetEntry, IHashEntry
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(sets);

        IReadOnlyCollection<RedisKey> keys = sets
            .Select(s => new RedisKey(s.Element.ToString()))
            .ToList()
            .AsReadOnly();

        ICollection<T?> hashes = await HashGetAllAsync<T>(db, keys);

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

    private static Task[] AddHashSetsToTransaction<T>(ITransaction tran, ICollection<T> values) where T : IHashEntry
    {
        ArgumentNullException.ThrowIfNull(tran);
        ArgumentNullException.ThrowIfNull(values);

        // TODO: If you await a transaction command, the connection 'hangs'. We should implement this pattern
        // lower down so it can be reused. https://stackoverflow.com/questions/25976231/stackexchange-redis-transaction-methods-freezes
        return values.Select(value =>
        {
            HashEntry[] hash = value.ToHashEntries();

            if (hash is not null)
            {
                return tran.HashSetAsync(value.Key, hash);
            }

            return Task.CompletedTask;

        }).ToArray();
    }

    #endregion
}
