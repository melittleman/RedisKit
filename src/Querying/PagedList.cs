using System.Collections.Generic;

using RedisKit.Querying.Abstractions;

namespace RedisKit.Querying;

public sealed class PagedList<T> : List<T>, IPagedList<T>
{
    public short CurrentPage { get; }

    public short TotalPages { get; }

    public long TotalResults { get; }

    public byte ResultsPerPage { get; }

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage < TotalPages;

    public PagedList(IEnumerable<T> items, long totalResults, SearchFilter filter)
    {
        TotalResults = totalResults;
        ResultsPerPage = filter.Count;
        CurrentPage = filter.Page;
        TotalPages = (short)Math.Ceiling(totalResults / (double)filter.Count);

        AddRange(items);
    }

    public PagedList(IPagedList<T> items)
    {
        TotalResults = items.TotalResults;
        ResultsPerPage = items.ResultsPerPage;
        CurrentPage = items.CurrentPage;
        TotalPages = items.TotalPages;

        AddRange(items);
    }
}
