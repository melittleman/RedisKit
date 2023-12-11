using System.Collections.Generic;

namespace NRedisKit.Querying.Abstractions;

public interface IPagedList<T> : IList<T>
{
    short CurrentPage { get; }

    short TotalPages { get; }

    long TotalResults { get; }

    byte ResultsPerPage { get; }

    bool HasPreviousPage { get; }

    bool HasNextPage { get; } 
}
