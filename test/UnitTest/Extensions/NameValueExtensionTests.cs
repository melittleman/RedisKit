using NRedisKit.Extensions;
using StackExchange.Redis;

namespace NRedisKit.UnitTest.Extensions;

public sealed class NameValueExtensionsTests
{
    [Fact]
    public void ToNameValueEntries_NullValue_ThrowsArgumentNullException()
    {
        // Arrange
        object? value = null;

        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => value.ToNameValueEntries());
    }

    [Fact]
    public void ToNameValueEntries_ReturnsExpectedResult()
    {
        // Arrange
        TestObject obj = new()
        { 
            Property1 = "Value1",
            Property2 = 123 
        };

        NameValueEntry[] expected =
        [
            new NameValueEntry("Property1", "Value1"),
            new NameValueEntry("Property2", "123")
        ];

        // Act
        var actual = obj.ToNameValueEntries();

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FromNameValueEntries_NullEntries_ThrowsArgumentNullException()
    {
        // Arrange
        NameValueEntry[]? entries = null;

        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => entries!.FromNameValueEntries<TestObject>());
    }

    [Fact]
    public void FromNameValueEntries_EmptyEntries_ReturnsDefault()
    {
        // Arrange
        NameValueEntry[] entries = [];

        // Act
        TestObject? actual = entries.FromNameValueEntries<TestObject>();

        // Assert
        Assert.Equal(default, actual);
    }

    [Fact]
    public void FromNameValueEntries_ReturnsExpectedResult()
    {
        // Arrange
        NameValueEntry[] entries =
        [
            new NameValueEntry("Property1", "Value1"),
            new NameValueEntry("Property2", "123") 
        ];

        TestObject expected = new()
        {
            Property1 = "Value1",
            Property2 = 123
        };

        // Act
        TestObject? actual = entries.FromNameValueEntries<TestObject>();

        // Assert
        Assert.Equivalent(expected, actual);
    }

    private class TestObject
    {
        public string? Property1 { get; set; }

        public int Property2 { get; set; }
    }
}
