namespace RedisKit.Abstractions;

public interface ISortedSetEntry
{
    string Member { get; set; }

    double Score { get; set; }
}
