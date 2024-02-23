using RedisKit.Querying.Enums;
using System.Text.Json.Serialization;

namespace RedisKit.Querying;

public sealed record SearchFilter
{
    // TODO: Maybe we should have a User defined default here instead?
    // Could be configured using an IOptions pattern,
    // but I think these defaults are sensible enough.
    private const byte MaxPageSize = 100;

    private byte _pageSize = 25;

    /// <summary>
    ///     The search term to apply and filter results by.
    /// </summary>
    /// <remarks>
    ///     If not specified, defaults to null which will
    ///     return all results in the indexed collection.
    /// </remarks>
    [JsonPropertyName("query")]
    public string? Query { get; set; } = null;

    /// <summary>
    ///     The page number of results to retrieve.
    /// </summary>
    /// <remarks>
    ///     If not specified, defaults to 1.
    /// </remarks>
    [JsonPropertyName("page")]
    public short Page { get; set; } = 1;

    /// <summary>
    ///     The number of results to return in each page.
    /// </summary>
    /// <remarks>
    ///     If not specified, defaults to 25.
    ///     Maximum allowed is 100.
    /// </remarks>
    [JsonPropertyName("count")]
    public byte Count
    {
        get => _pageSize;

        set => _pageSize = value > MaxPageSize
            ? MaxPageSize
            : value;
    }

    /// <summary>
    ///     The property or field name to order results by.
    /// </summary>
    /// <remarks>
    ///     If not specified, defaults to null and will be 
    ///     returned in the same order they are retrieved.
    /// </remarks>
    [JsonPropertyName("order_by")]
    public string? OrderBy { get; set; } = null;

    /// <summary>
    ///     The direction to sort the <see cref="OrderBy"/> field.
    /// </summary>
    /// <remarks>
    ///     For example; order by created date descending
    ///     to prioritize newly created results first.
    ///     
    ///     If not specified, defaults to <see cref="SortDirection.Ascending" />
    /// </remarks>
    [JsonPropertyName("sort_by")]
    public SortDirection SortBy { get; set; } = SortDirection.Ascending;

    public SearchFilter() { }

    // TODO: Should these contructors also provide the default values?
    public SearchFilter(byte count)
    {
        Count = count;
    }

    public SearchFilter(short page, byte count)
    {
        Page = page;
        Count = count;
    }

    public SearchFilter(short page, byte count, string query)
    {
        Page = page;
        Count = count;
        Query = query;
    }

    public SearchFilter(
        short page, 
        byte count, 
        string query, 
        string orderBy)
    {
        Page = page;
        Count = count;
        Query = query;
        OrderBy = orderBy;
    }

    public SearchFilter(
        short page, 
        byte count, 
        string query, 
        string orderBy, 
        SortDirection sortBy)
    {
        Page = page;
        Count = count;
        Query = query;
        OrderBy = orderBy;
        SortBy = sortBy;
    }
}
