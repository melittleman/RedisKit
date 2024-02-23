using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

using NRedisStack;
using NRedisStack.Search;
using NRedisStack.Search.DataTypes;

using RedisKit.Querying.Enums;
using RedisKit.Querying.Abstractions;

namespace RedisKit.Querying.Extensions;

public static class SearchCommandsExtensions
{
    public static async Task<bool> CreateIndexAsync(
        this SearchCommands search,
        string indexName,
        FTCreateParams createParams,
        Schema schema,
        bool forceRecreate = false)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(indexName);
        ArgumentNullException.ThrowIfNull(createParams);
        ArgumentNullException.ThrowIfNull(schema);

        // I think there's potentially a way to update schemas within an index,
        // but for the time being we will just drop the whole index if we need to
        // force a re-creation (due to schema changes within the document).
        if (forceRecreate)
        {
            try
            {
                await search.DropIndexAsync(indexName);
            }
            catch (RedisServerException rse) when (rse.Message is "Unknown Index name")
            {
                // Index already doesn't exist...
            }
        }

        // We can return true here if the Index already exists
        // as we weren't asked to forcibly recreate it.
        if (await IndexExistsAsync(search, indexName)) return true;

        try
        {
            return await search.CreateAsync(indexName, createParams, schema);
        }
        catch (RedisServerException rse) when (rse.Message is "Index already exists")
        {
            // Index already exists, another process must have just got there?
            return true;
        }
    }

    public static async Task<IPagedList<T>> SearchAsync<T>(
        this SearchCommands search,
        string indexName,
        SearchFilter filter,
        params string[] highlightFields)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(indexName);
        ArgumentNullException.ThrowIfNull(filter);

        Query query = GetPagedQuery(filter, false, highlightFields);

        try
        {
            SearchResult result = await search.SearchAsync(indexName, query);

            return result?.Documents is not null
                ? ConvertTo<T>(result.Documents).ToPagedList(result.TotalResults, filter)
                : new PagedList<T>(Array.Empty<T>(), 0, filter);
        }
        catch (Exception)
        {
            return new PagedList<T>(Array.Empty<T>(), 0, filter);
        }
    }

    public static async Task<T?> SearchSingleAsync<T>(this SearchCommands search, string indexName, string searchQuery)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(indexName);
        ArgumentNullException.ThrowIfNull(searchQuery);

        SearchFilter filter = new(page: 1, count: 1, query: searchQuery);

        // TODO: try catch...

        SearchResult result = await search.SearchAsync(indexName, GetPagedQuery(filter));

        return result?.Documents is not null
            ? ConvertTo<T>(result.Documents).SingleOrDefault()
            : default;
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
    public static async Task<ICollection<T>> SearchAllAsync<T>(this SearchCommands search, string indexName, SearchFilter? filter = null)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(indexName);

        // Provide a default value of starting at
        // page 1, with 100 results per page.
        filter ??= new SearchFilter(page: 1, count: 100);

        // TODO: try catch...

        IPagedList<T> results = await search.SearchAsync<T>(indexName, filter);

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
            results = await search.SearchAsync<T>(indexName, filter);

            documents.AddRange(results);
        }

        return documents;
    }

    private static async Task<bool> IndexExistsAsync(SearchCommands search, string indexName)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(indexName);

        try
        {
            InfoResult result = await search.InfoAsync(indexName);

            // TODO: we can use InfoResult.Attributes to see if the current index matches?!

            return result.IndexName.Equals(indexName, StringComparison.Ordinal);
        }
        catch (RedisServerException rse) when (rse.Message is "Unknown Index name")
        {
            // Index doesn't exist
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static Query GetPagedQuery(
        SearchFilter filter,
        bool summarize = false,
        params string[] highlightFields)
    {
        ArgumentNullException.ThrowIfNull(filter);

        Query builder;

        if (filter.OrderBy is not null)
        {
            builder = GetSortedQuery(filter, summarize, highlightFields);
        }
        else
        {
            builder = GetDefaultQuery(filter.Query, summarize, highlightFields);
        }

        return builder.Limit((filter.Page - 1) * filter.Count, filter.Count);
    }

    private static Query GetSortedQuery(
        SearchFilter filter,
        bool summarize = false,
        params string[] highlights)
    {
        ArgumentNullException.ThrowIfNull(filter);

        Query builder = GetDefaultQuery(filter.Query, summarize, highlights);

        return filter.OrderBy is not null
            ? builder.SetSortBy(filter.OrderBy, filter.SortBy is SortDirection.Ascending)
            : builder;
    }

    private static Query GetDefaultQuery(
        string? searchQuery,
        bool summarize = false,
        params string[] highlights)
    {
        Query builder = new(searchQuery ?? "*");

        if (highlights.Length > 0)
        {
            // https://oss.redislabs.com/redisearch/Highlight/#highlighting
            builder.HighlightFields(new Query.HighlightTags("<span class=\"query-found\">", "</span>"), highlights);
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

    private static List<T> ConvertTo<T>(ICollection<Document> documents)
    {
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

        return results;
    }
}
