using NRedisKit.Extensions;
using StackExchange.Redis;

namespace NRedisKit.UnitTest;

public sealed partial class RedisClientTests
{
    public class TestHashObject
    {
        public string? Name { get; set; }

        public int Age { get; set; }
    }

    [Fact]
    public async Task GetFromHashAsync_WithNullKey_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _redisClient.GetFromHashAsync<TestJsonObject>(null!));
    }

    [Fact]
    public async Task GetFromHashAsync_ShouldReturnNull_WhenKeyDoesNotExist()
    {
        // Arrange
        string key = "non-existing-key";

        // Act
        TestHashObject? result = await _redisClient.GetFromHashAsync<TestHashObject>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetFromHashAsync_ShouldReturnExpectedValue_WhenKeyExists()
    {
        // Arrange
        string key = "hash-key";
        TestHashObject expectedValue = new() { Name = "John", Age = 30 };
        HashEntry[] expectedResult = expectedValue.ToHashEntries();

        _mockDatabase
            .Setup(db => db.HashGetAllAsync(key, It.IsAny<CommandFlags>()))
            .ReturnsAsync(expectedResult);

        // Act
        TestHashObject? actualValue = await _redisClient.GetFromHashAsync<TestHashObject>(key);

        // Assert
        Assert.NotNull(actualValue);
        Assert.IsType<TestHashObject>(actualValue); 
        Assert.Equivalent(expectedValue, actualValue);

        Assert.Equal(expectedValue.Name, actualValue.Name);
        Assert.Equal(expectedValue.Age, actualValue.Age);
    }

    [Fact]
    public async Task GetAllFromHashesAsync_WithNullKey_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _redisClient.GetAllFromHashesAsync<TestJsonObject>(null!));
    }

    [Fact]
    public async Task GetAllFromHashesAsync_ShouldReturnEmptyCollection_WhenKeysDoNotExist()
    {
        // Arrange
        string[] keys = ["non-existing-key-1", "non-existing-key-2"];

        Mock<IBatch> mockBatch = new();

        _mockDatabase
            .Setup(db => db.CreateBatch(It.IsAny<object>()))
            .Returns(mockBatch.Object);

        // Act
        ICollection<TestHashObject?> result = await _redisClient.GetAllFromHashesAsync<TestHashObject>(keys);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllFromHashesAsync_ShouldReturnExpectedValues_WhenKeysExist()
    {
        // Arrange
        Dictionary<string, TestHashObject> expectedValues = new()
        {
            { "hash-key-1", new TestHashObject() { Name = "John", Age = 30 } },
            { "hash-key-2", new TestHashObject() { Name = "Jane", Age = 40 } }
        };

        Mock<IBatch> mockBatch = new();

        for (int i = 0; i < expectedValues.Count; i++)
        {
            string key = expectedValues.Keys.ElementAt(i);

            mockBatch
                .Setup(mb => mb.HashGetAllAsync(key, It.IsAny<CommandFlags>()))
                .ReturnsAsync(expectedValues[key].ToHashEntries());
        }

        _mockDatabase
            .Setup(db => db.CreateBatch(It.IsAny<object>()))
            .Returns(mockBatch.Object);

        // Act
        ICollection<TestHashObject?> results = await _redisClient.GetAllFromHashesAsync<TestHashObject>(expectedValues.Keys);

        // Assert
        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.Equal(expectedValues.Count, results.Count);

        for (int i = 0; i < expectedValues.Count; i++)
        {
            string key = expectedValues.Keys.ElementAt(i);
            TestHashObject? thisResult = results.ElementAt(i);

            Assert.IsType<TestHashObject>(thisResult);
            Assert.Equivalent(expectedValues[key], thisResult);

            Assert.Equal(expectedValues[key].Name, thisResult?.Name);
            Assert.Equal(expectedValues[key].Age, thisResult?.Age);
        }
    }
}
