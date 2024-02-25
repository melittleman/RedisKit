using Microsoft.AspNetCore.Identity;

namespace RedisKit.DependencyInjection.Options;

public sealed record RedisAuthenticationTicketOptions
{
    public string KeyPrefix { get; set; } = "auth-tickets:";

    public string CookieSchemeName { get; set; } = IdentityConstants.ApplicationScheme;
}
