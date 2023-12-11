using System.Threading.Tasks;
using System.Threading;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NRedisKit.Diagnostics;

/// <inheritdoc />
internal sealed record RedisHealthCheck : IHealthCheck
{
    private readonly IRedisContext _redis;
    private readonly ILogger<RedisHealthCheck> _logger;

    public RedisHealthCheck(
        IRedisContext redis,
        ILogger<RedisHealthCheck> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        if (_redis.Connection.IsConnected)
        {
            _logger.LogDebug("Redis health check is healthy. Currently connected.");

            return Task.FromResult(HealthCheckResult.Healthy("Redis is connected."));
        }

        // ReSharper disable once ConvertIfStatementToReturnStatement
        // ML: Despite being slightly longer, we don't need to chain 3 ternary's here.
        if (_redis.Connection.IsConnecting)
        {
            _logger.LogWarning("Redis health check is degraded. Currently connecting...");

            return Task.FromResult(HealthCheckResult.Degraded("Redis is connecting."));
        }

        _logger.LogError("Redis health check is unhealthy. Currently not connected.");

        return Task.FromResult(HealthCheckResult.Unhealthy("Redis is not connected."));
    }
}
