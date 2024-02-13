using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

using RedisKit.DependencyInjection;
using RedisKit.DependencyInjection.Options;

namespace RedisKit.UnitTest.DependencyInjection;

public sealed class RedisConnectionProviderTests
{
    private readonly DefaultRedisConnectionProvider _provider;

    public RedisConnectionProviderTests()
    {
        Mock<IOptionsMonitor<RedisConnectionOptions>> options = new();
        Mock<ILoggerFactory> loggers = new();
        Mock<IHostEnvironment> env = new();

        _provider = new DefaultRedisConnectionProvider(
            options.Object,
            loggers.Object,
            env.Object);
    }

    [Fact]
    public void GetRequiredConnection_WithNullName_ShouldThrowArgumentException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => _provider.GetRequiredConnection(null!));
    }

    [Fact]
    public void GetRequiredConnection_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => _provider.GetRequiredConnection(""));
    }

    [Fact]
    public void GetRequiredConnection_WithUnknownConnection_ShouldThrowInvalidOperationException()
    {
        // Arrange, Act & Assert
        Assert.Throws<InvalidOperationException>(() => _provider.GetRequiredConnection("unknown"));
    }
}
