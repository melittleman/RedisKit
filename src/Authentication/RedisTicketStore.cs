using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

using IdentityModel;

using NRedisStack;
using NRedisStack.RedisStackCommands;

namespace NRedisKit.Authentication;

/// <summary>
///     A custom <see cref="AuthenticationTicket"/> storage implementation of <see cref="ITicketStore"/> using
///     a <see cref="RedisContext"/> to persist the data into distributed server-side cache as JSON.
/// </summary>
public sealed record RedisTicketStore : ITicketStore
{
    private const string SessionId = "session_id";

    private readonly RedisAuthenticationTicketOptions _options;
    private readonly IRedisContext _redis;

    private IDatabase Db => _redis.Db;

    private JsonCommands Json => Db.JSON();

    private string KeyPrefix => _options.KeyPrefix;

    public RedisTicketStore(IRedisContext redis, RedisAuthenticationTicketOptions options)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        if (ticket is null) throw new ArgumentNullException(nameof(ticket));

        string key = string.Empty;

        // The ClaimsPrincipal is more likely to have the 'sid' claim present as this
        // would have been issued in the id_token so prioritize checking in here first.
        if (ticket.Principal.HasClaim(claim => claim.Type == JwtClaimTypes.SessionId))
        {
            key = GetKey(ticket.Principal.FindFirst(JwtClaimTypes.SessionId)?.Value);
        }
        else if (ticket.Properties.Items.ContainsKey(SessionId))
        {
            key = GetKey(ticket.Properties.Items[SessionId]);
        }

        await RenewAsync(key, ticket);

        return key;
    }

    /// <inheritdoc />
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        if (ticket is null) throw new ArgumentNullException(nameof(ticket));
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Cannot be null or empty.", nameof(key));

        string redisKey = GetKey(key);

        if (await Json.SetAsync(redisKey, "$", ticket))
        {
            if (ticket.Properties.ExpiresUtc.HasValue)
            {
                Db.KeyExpire(redisKey, ticket.Properties.ExpiresUtc.Value.UtcDateTime);
            }
        }
    }

    /// <inheritdoc />
    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Cannot be null or empty.", nameof(key));

        return Json.GetAsync<AuthenticationTicket>(GetKey(key));
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Cannot be null or empty.", nameof(key));

        AuthenticationTicket? ticket = await RetrieveAsync(key);

        // Ticket has already been cleared out by some other means?
        // Potentially due to a TTL expiration of the cache key.
        // Either way, the User is now successfully logged out!
        if (ticket is null) return;

        if (string.IsNullOrWhiteSpace(ticket.Properties.GetTokenValue(OidcConstants.TokenTypes.RefreshToken)))
        {
            // We can only remove an "Authentication Ticket" when it does NOT have the 'refresh_token'
            // present, as this needs to go through the token revocation process on logout.
            await Db.KeyDeleteAsync(GetKey(key));
        }
    }

    #region Private Methods

    private string GetKey(string? key) => string.IsNullOrWhiteSpace(key) is false && key?.StartsWith(KeyPrefix) is true
        ? key
        : KeyPrefix + key;

    #endregion
}
