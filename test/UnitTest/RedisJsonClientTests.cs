namespace NRedisKit.UnitTest;

public sealed partial class RedisClientTests
{
    public class TestJsonObject 
    { 
        public string? Name { get; set; }

        public int Age { get; set; }
    }

    [Fact]
    public async Task GetFromJsonAsync_WithNullKey_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _redisClient.GetFromJsonAsync<TestJsonObject>(null!));
    }

    // Turns out the NReJson library is actually very un-mockable...
    // Not too sure what we're going to do about this one for the time being

    //[Fact]
    //public async Task GetFromJsonAsync_ValidKey_ReturnsDeserializedValue()
    //{
    //    // Arrange
    //    string key = "valid-key";
    //    TestJsonObject expectedValue = new() { Name = "John Doe", Age = 30 };
    //    string serializedValue = JsonSerializer.Serialize(expectedValue);
    //    RedisResult expectedResult = RedisResult.Create(new RedisKey(serializedValue));

    //    //PathedResult<TestJsonObject> jsonResult = new PathedResult<TestJsonObject>());
    //    PathedResult<TestJsonObject> jsonResult = It.IsAny<PathedResult<TestJsonObject>>();
    //    Mock<PathedResult<TestJsonObject>> mockResult = new();

    //    NReJSONSerializer.SerializerProxy = _mockSerializer.Object;
    //    _mockDatabase.Setup(db => db.ExecuteAsync("JSON.GET")).ReturnsAsync(expectedResult);

    //    _mockDatabase
    //        .Setup(r => r.JsonGetAsync<TestJsonObject>(key, It.IsAny<string[]>()))
    //        .ReturnsAsync(jsonResult);

    //    _mockSerializer.Setup(s => s.Deserialize<TestJsonObject>(expectedResult)).Returns(expectedValue);

    //    // Act
    //    var actualValue = await _redisClient.GetFromJsonAsync<object>(key);

    //    // Assert
    //    Assert.Equal(expectedValue, actualValue);

    //    _mockDatabase.Verify(r => r.JsonGetAsync<TestJsonObject>(key, It.IsAny<string[]>()), Times.Once);
    //    _mockSerializer.Verify(s => s.Deserialize<TestJsonObject>(expectedResult), Times.Once);
    //}

    //[Fact]
    //public async Task GetFromJsonArrayAsync_WithValidKey_ReturnsDeserializedValue()
    //{
    //    // Arrange
    //    string key = "valid-key";
    //    ICollection<TestJsonObject> expectedValue = new List<TestJsonObject>()
    //    {
    //        new TestJsonObject { Name = "John Doe", Age = 30 },
    //        new TestJsonObject { Name = "Jane Doe", Age = 28 },
    //    };

    //    string serializedValue = JsonSerializer.Serialize(expectedValue);
    //    RedisResult expectedResult = RedisResult.Create(new RedisValue(serializedValue));

    //    _mockSerializer.Setup(s => s.Deserialize<ICollection<TestJsonObject>>(expectedResult)).Returns(expectedValue);
    //    NReJSONSerializer.SerializerProxy = _mockSerializer.Object;

    //    _mockDatabase
    //        .Setup(db => db.ExecuteAsync("JSON.GET", It.IsAny<object[]>(), CommandFlags.None))
    //        .ReturnsAsync(expectedResult);

    //    // Act
    //    ICollection<TestJsonObject>? actualValue = await _redisClient.GetFromJsonArrayAsync<TestJsonObject>(key);

    //    // Assert
    //    Assert.Equal(expectedValue, actualValue);
    //}
}
