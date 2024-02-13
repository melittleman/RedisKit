using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

using IdentityModel;

using NRedisStack;
using NRedisStack.RedisStackCommands;

namespace RedisKit.Authentication;

/// <summary>
///     A custom <see cref="AuthenticationTicket"/> storage implementation of <see cref="ITicketStore"/> using
///     a <see cref="RedisConnection"/> to persist the data into distributed server-side cache as JSON.
/// </summary>
public sealed record RedisTicketStore : ITicketStore
{
    private const string SessionId = "session_id";

    private readonly RedisAuthenticationTicketOptions _ticketOptions;
    private readonly RedisJsonOptions _jsonOptions;
    private readonly IRedisConnection _redis;

    private IDatabase Db => _redis.Db;

    private JsonCommands Json => Db.JSON();

    private string KeyPrefix => _ticketOptions.KeyPrefix;

    public RedisTicketStore(
        IRedisConnection redis,
        RedisAuthenticationTicketOptions ticketOptions,
        RedisJsonOptions jsonOptions)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _ticketOptions = ticketOptions ?? throw new ArgumentNullException(nameof(ticketOptions));

        // The RedisJsonOptions 'should' always contain an instance of AuthenticationTicketJsonConverter
        // if the .AddRedisTicketStore extension was used, but should we actually be checking this?
        _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        string key = string.Empty;

        // The ClaimsPrincipal is more likely to have the 'sid' claim present as this
        // would have been issued in the id_token so prioritize checking in here first.
        if (ticket.Principal.HasClaim(claim => claim.Type == JwtClaimTypes.SessionId))
        {
            key = GetKey(ticket.Principal.FindFirst(JwtClaimTypes.SessionId)?.Value);
        }
        else if (ticket.Properties.Items.TryGetValue(SessionId, out string? value) && value is not null)
        {
            key = GetKey(value);
        }

        await RenewAsync(key, ticket);

        return key;
    }

    /// <inheritdoc />
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        string redisKey = GetKey(key);

        if (await Json.SetAsync(redisKey, "$", ticket, serializerOptions: _jsonOptions.Serializer))
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
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return Json.GetAsync<AuthenticationTicket>(GetKey(key), serializerOptions: _jsonOptions.Serializer);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

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
