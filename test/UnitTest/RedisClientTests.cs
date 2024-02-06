using Microsoft.Extensions.Logging;
using StackExchange.Redis;

using RedisKit.DependencyInjection.Options;
using RedisKit.Abstractions;

namespace RedisKit.UnitTest;

public sealed partial class RedisClientTests
{
    private readonly RedisClient _redisClient;
    private readonly Mock<IDatabase> _mockDatabase;

    public RedisClientTests()
    {
        _mockDatabase = new Mock<IDatabase>();

        Mock<IConnectionMultiplexer> mockConnection = new();

        Mock<IRedisContext> mockContext = new();
        mockContext.Setup(c => c.Db).Returns(_mockDatabase.Object);
        mockContext.Setup(c => c.Connection).Returns(mockConnection.Object);

        Mock<ILoggerFactory> mockLoggerFactory = new();
        Mock<ILogger<RedisClient>> mockLogger = new();

        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(() => mockLogger.Object);

        _redisClient = new RedisClient(
            mockLoggerFactory.Object,
            mockContext.Object,
            new RedisJsonOptions());
    }

    [Fact]
    public async Task DeleteAllAsync_DeletesAllExistingKeys_ReturnsTrue()
    {
        // Arrange
        string[] keys = ["existingKey1", "existingKey2", "existingKey3"];
        RedisKey[] redisKeys = keys.Select(k => new RedisKey(k)).ToArray();

        _mockDatabase
            .Setup(redis => redis.KeyDeleteAsync(redisKeys, It.IsAny<CommandFlags>()))
            .ReturnsAsync(3);

        // Act
        bool result = await _redisClient.DeleteAllAsync(keys);

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(redis => redis.KeyDeleteAsync(redisKeys, It.IsAny<CommandFlags>()), Times.Once);
    }
}
