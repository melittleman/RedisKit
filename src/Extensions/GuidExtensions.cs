namespace NRedisKit.Extensions;

public static class GuidExtensions
{
    public static string ToEscapedString(this Guid source) => source.ToString().Replace("-", "\\-");
}
