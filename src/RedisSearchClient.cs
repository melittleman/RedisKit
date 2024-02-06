using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

using NRedisStack;
using NRedisStack.Search;
using NRedisStack.Search.DataTypes;
using NRedisStack.RedisStackCommands;

using RedisKit.Querying.Abstractions;
using RedisKit.Querying.Extensions;
using RedisKit.Querying.Enums;
using RedisKit.Querying;

namespace RedisKit;

/// <inheritdoc />
public sealed partial record RedisClient
{
    // TODO: Investigate moving Index creation/synchronization into a Hosted Background Service...
    // This seems to be the way that the Redis.OM library recommends this is handled.

    public SearchCommands Search => Db.FT();

    public async Task<bool> CreateIndex(
        string indexName,
        Schema schema,
        FTCreateParams createParams,
        bool forceRecreate = false)
    {
        _logger.LogTrace("Creating index {Name} with forceRecreate {Recreate}", indexName, forceRecreate);

        try
        {
            if (forceRecreate)
            {
                // I think there's potentially a way to 'update' Schemas within an Index,
                // but for the time being we will just drop the whole Index if we need to
                // force a re-creation (likely due to Schema changes within the Document).
                if (await Search.DropIndexAsync(indexName) is false) return false;
            }

            // We can return true here as the Index already exists
            if (await IndexExists(indexName)) return true;

            // Either the Index doesn't exist because we just dropped due to a 'force recreate'
            // OR
            // The Index didn't exist in the first place.
            // We then need to create this Index.
            return await Search.CreateAsync(indexName, createParams, schema);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Creating index {Name} failed", indexName);
            return false;
        }
    }

    private async Task<bool> IndexExists(string indexName)
    {
        _logger.LogTrace("Checking index {Name} exists", indexName);

        try
        {
            InfoResult result  = await Search.InfoAsync(indexName);

            // TODO: we can use InfoResult.Attributes to see if the current index matches?!

            return result.IndexName.Equals(indexName, StringComparison.Ordinal);
        }
        catch (RedisServerException rse) when (rse.Message is "Unknown Index name")
        {
            // Index doesn't exist
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checking index {Name} failed", indexName);
            return false;
        }
    }

    /// <summary>
    ///     Uses the default wildcard value "*" to return all
    ///     documents contained within the search index.
    /// </summary>
    /// <typeparam name="T">The type of document to search the index of</typeparam>
    /// <param name="filter">
    ///     An optional filter to define the page number, count and order for which to return results.
    ///     If not provided, will default to page 1 with a count of 100 per page.
    /// </param>
    /// <returns>
    ///     The entire collection of indexed documents of type <typeparamref name="T"/>.
    /// </returns>
    /// <remarks>
    ///     This has the potential to be a long running operation depending on the number
    ///     of documents contained within the index. This will gradually step through each
    ///     page to build up the result set.
    /// </remarks>
    public async Task<ICollection<T>> SearchAllAsync<T>(string indexName, SearchFilter? filter = null)
    {
        // Provide a default value of starting at
        // page 1, with 100 results per page.
        filter ??= new SearchFilter(1, 100);
        
        IPagedList<T> results = await SearchAsync<T>(indexName, "*", filter);

        // We must have managed to retrieve all results
        // in the first page, return them as-is.
        if (results.HasNextPage is false) return results;

        // Otherwise we need to loop over and build up the entire result set.
        List<T> documents = [.. results];

        while (documents.Count < results.TotalResults)
        {
            if (results.HasNextPage is false) break;

            // Request the next page
            filter.Page++;

            // There's a hard stop if we go beyond the
            // total number of pages, although this should
            // theoretically never happen!
            if (filter.Page > results.TotalPages) break;

            // TODO: These could probably all be pipelined into
            // a transaction and execute as a batch?
            // Not too worried at the moment as it's unlikely we'll
            // have thousands of results right now at least.
            results = await SearchAsync<T>(indexName, "*", filter);

            documents.AddRange(results);
        }

        return documents;
    }

    public async Task<IPagedList<T>> SearchAsync<T>(
        string indexName,
        string searchTerm,
        SearchFilter filter,
        params string[] highlightFields)
    {
        if (searchTerm is null) throw new ArgumentNullException(nameof(searchTerm));

        _logger.LogTrace("Searching index {Name} with query {Term}",
            indexName,
            searchTerm);

        Query query = GetPagedQuery(searchTerm, filter, false, highlightFields);

        try
        {
            SearchResult result = await Search.SearchAsync(indexName, query);

            return result?.Documents is not null
                ? ConvertTo<T>(result.Documents).ToPagedList(result.TotalResults, filter)
                : new PagedList<T>(Array.Empty<T>(), 0, filter);
        }
        catch (RedisServerException rse) when (rse.Message.Contains("Syntax error"))
        {
            // This exception gets thrown from 'Syntax errors' such as a Redis Query containing
            // reserved terms & keywords i.e. OFF, DEL, %, > etc...
            // We DO NOT need to sanitize however as Redis handles this internally during tokenization.
            _logger.LogWarning(rse, "Syntax error when searching index {Name} with query {Query}",
                indexName,
                query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Searching index {Name} with query {Query} failed",
                indexName,
                query);
        }

        return new PagedList<T>(Array.Empty<T>(), 0, filter);
    }

    // TODO: This should probably also return an IPagedList as
    // it will likely not contain all the results by default.
    public async Task<ICollection<T>> SearchAsync<T>(string indexName, Query query)
    {
        _logger.LogTrace("Searching index {Name} with query {Query}",
            indexName,
            query.QueryString);

        try
        {
            SearchResult result = await Search.SearchAsync(indexName, query);

            return result?.Documents is not null
                ? ConvertTo<T>(result.Documents)
                : Array.Empty<T>();
        }
        catch (RedisServerException rse) when (rse.Message.Contains("Syntax error"))
        {
            // This exception gets thrown from 'Syntax errors' such as a Redis Query containing
            // reserved terms & keywords i.e. OFF, DEL, %, > etc...
            // We DO NOT need to sanitize however as Redis handles this internally during tokenization.
            _logger.LogWarning(rse, "Syntax error when searching index {Name} with query {Query}",
                indexName,
                query.QueryString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Searching index {Name} with query {Query} failed",
                indexName,
                query.QueryString);
        }

        return Array.Empty<T>();
    }

    // TODO: This should probably also return an IPagedList as
    // it will likely not contain all the results by default.
    public Task<ICollection<T>> SearchAsync<T>(string indexName, string searchTerm)
    {
        _logger.LogTrace("Searching index {Name} with query {Term}",
            indexName,
            searchTerm);

        return SearchAsync<T>(indexName, GetDefaultQuery(searchTerm));
    }

    public async Task<T?> SearchSingleAsync<T>(string indexName, string searchTerm)
    {
        _logger.LogTrace("Searching index {Name} with query {Term}",
            indexName,
            searchTerm);

        ICollection<T> results = await SearchAsync<T>(indexName, GetPagedQuery(searchTerm, new SearchFilter(1, 1)));

        // TODO: Single vs First ?
        return results.SingleOrDefault();
    }

    private ICollection<T> ConvertTo<T>(ICollection<Document> documents)
    {
        _logger.LogTrace("Converting {Count} RediSearch documents", documents.Count);

        List<T> results = [];

        foreach (Document document in documents)
        {
            T? result;

            // "json" seems to be the key that Redis gives us
            // which contains the entire JSON string value.
            // TODO: This might not actually be what we want?
            // i.e. The Query might only be returning certain
            // fields so probably need to pass the JSON path of
            // what we want to retrieve into this method.
            string? json = document["json"];

            if (json is not null)
            {
                // This indexed document is stored as JSON
                result = JsonSerializer.Deserialize<T>(json);
            }
            else
            {
                // This indexed document is stored as a Hash
                result = document.FromSearchDocument<T>();
            }

            if (result is not null)
            {
                results.Add(result);
            }
        }

        _logger.LogTrace("Returning {Count} RediSearch results", results.Count);

        return results;
    }

    private static Query GetDefaultQuery(
        string searchTerm,
        bool summarize = false,
        params string[] highlights)
    {
        Query builder = new(searchTerm);

        if (highlights.Any())
        {
            // https://oss.redislabs.com/redisearch/Highlight/#highlighting
            builder.HighlightFields(new Query.HighlightTags("<span class=\"search-term-found\">", "</span>"), highlights);
        }

        // https://oss.redislabs.com/redisearch/Highlight/#summarization
        // This causes an issue with HTML content as it cuts off halfway through a tag.
        // Therefore can only be used when we are certain the entity fields do NOT contain any HTML.
        if (summarize)
        {
            builder.SummarizeFields(25, 2, "..."); // TODO: Pass in the fields explicitly
        }

        return builder;
    }

    private static Query GetSortedQuery(
        string searchTerm,
        SearchFilter filter,
        bool summarize = false,
        params string[] highlights)
    {
        Query builder = GetDefaultQuery(searchTerm, summarize, highlights);

        return filter.OrderBy is not null 
            ? builder.SetSortBy(filter.OrderBy, filter.SortBy is SortDirection.Ascending)
            : builder;
    }

    private static Query GetPagedQuery(
        string searchTerm,
        SearchFilter filter,
        bool summarize = false,
        params string[] highlightFields)
    {
        Query builder;

        if (filter.OrderBy is not null)
        {
            builder = GetSortedQuery(searchTerm, filter, summarize, highlightFields);
        }
        else
        {
            builder = GetDefaultQuery(searchTerm, summarize, highlightFields);
        }

        return builder.Limit((filter.Page - 1) * filter.Count, filter.Count);
    }
}
