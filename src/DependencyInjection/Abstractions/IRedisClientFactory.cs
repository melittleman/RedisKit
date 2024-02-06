namespace RedisKit.DependencyInjection.Abstractions;

public interface IRedisClientFactory
{
    RedisClient CreateClient(string name);
}
