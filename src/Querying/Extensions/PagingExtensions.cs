using System.Linq;
using System.Collections.Generic;

using RedisKit.Querying.Abstractions;

namespace RedisKit.Querying.Extensions;

public static class PagingExtensions
{
    public static IPagedList<T> ToPagedList<T>(
        this IEnumerable<T> source,
        long totalResults,
        short currentPage,
        byte resultsPerPage)
    {
        ArgumentNullException.ThrowIfNull(source);

        SearchFilter filter = new(currentPage, resultsPerPage);

        return source.ToPagedList(totalResults, filter);
    }

    public static IPagedList<T> ToPagedList<T>(this IEnumerable<T> source, long totalResults, SearchFilter filter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(filter);

        IList<T> items = source.ToList();

        return items.ToPagedList(totalResults, filter);
    }

    public static IPagedList<T> ToPagedList<T>(this ICollection<T> source, long totalResults, SearchFilter filter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(filter);

        IList<T> items = source.ToList();

        return items.ToPagedList(totalResults, filter);
    }

    public static IPagedList<T> ToPagedList<T>(this IList<T> source, long totalResults, SearchFilter filter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(filter);

        return source.Count <= filter.Count
            ? new PagedList<T>(source, totalResults, filter)
            : PageList(source, totalResults, filter); // We have too many results? Re-page the input
    }

    public static PagedList<T> ToPagedList<T>(this IPagedList<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // An implicit / explicit operator for this
        // conversion might also be quite useful?
        return new PagedList<T>(source);
    }

    private static PagedList<T> PageList<T>(IList<T> source, long totalResults, SearchFilter filter)
    {
        IEnumerable<T> pages = source
            .Skip((filter.Page - 1) * filter.Count)
            .Take(filter.Count);

        return new PagedList<T>(pages, totalResults, filter);
    }
}
