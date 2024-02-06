namespace RedisKit.DependencyInjection.Options;

public sealed record RedisDataProtectionOptions
{
    public string KeyName { get; set; } = "data-protection:keys";

    public string? ApplicationIsolation { get; set; }
}
