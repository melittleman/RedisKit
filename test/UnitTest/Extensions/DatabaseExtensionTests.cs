using RedisKit.Extensions;
using StackExchange.Redis;

namespace RedisKit.UnitTest.Extensions;

public sealed class DatabaseExtensionTests
{
    public class TestHashObject
    {
        public string? Name { get; set; }

        public int Age { get; set; }
    }

    [Fact]
    public async Task HashGetAsync_ReturnsValue_WhenKeyExists()
    {
        // Arrange
        Mock<IDatabase> mockDatabase = new();
        string key = "testKey";

        HashEntry[] hashEntries =
        [
            new("Name", "Dave"),
            new("Age", 33),
        ];

        mockDatabase.Setup(db => db.HashGetAllAsync(key, CommandFlags.None)).ReturnsAsync(hashEntries);

        // Act
        TestHashObject? result = await mockDatabase.Object.HashGetAsync<TestHashObject>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Dave", result.Name);
        Assert.Equal(33, result.Age);
    }

    [Fact]
    public async Task HashGetAsync_ThrowsException_WhenDatabaseIsNull()
    {
        // Arrange
        IDatabase db = null!; // Simulating null database

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => db.HashGetAsync<TestHashObject>("testKey"));
    }
}
