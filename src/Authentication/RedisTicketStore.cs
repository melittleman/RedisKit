using System.Text.Json;
using System.Globalization;
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
    // RFC 1123 DateTime format
    private const string UtcDateTimeFormat = "r";
    private const string SessionIdKey = "session_id";
    private const string LastActivityUtcKey = ".last_activity";

    // TODO: Should 'RedisJsonOptions' actually just be
    // included as part of the TicketOptions itself?
    private readonly RedisAuthenticationTicketOptions _ticketOptions;
    private readonly RedisJsonOptions _jsonOptions;
    private readonly IRedisConnection _redis;

    private JsonNamingPolicy Naming => _jsonOptions.Serializer.PropertyNamingPolicy
        ?? JsonSerializerOptions.Default.PropertyNamingPolicy
        ?? JsonNamingPolicy.SnakeCaseLower;

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
        else if (ticket.Properties.Items.TryGetValue(SessionIdKey, out string? value) && value is not null)
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

        SetLastActivity(ticket, DateTimeOffset.UtcNow);

        // TODO: Implement a pipeline for these two requests, not super important
        // though as this is a very 'cold' path that is not frequently used.
        if (await Json.SetAsync(redisKey, "$", ticket, serializerOptions: _jsonOptions.Serializer))
        {
            if (ticket.Properties.ExpiresUtc.HasValue)
            {
                Db.KeyExpire(redisKey, ticket.Properties.ExpiresUtc.Value.UtcDateTime);
            }
        }
    }

    /// <inheritdoc />
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        string properties = Naming.ConvertName(nameof(AuthenticationTicket.Properties));
        string items = Naming.ConvertName(nameof(AuthenticationTicket.Properties.Items));

        // TODO: You could argue that this property name conversion isn't actually required, because simply using
        // the JSON Path "$..['.last_activity'] would also find it, but there's more of a clashing risk with this.
        Task<bool> setTask = Json.SetAsync(
            GetKey(key), 
            $"$.{properties}.{items}['{LastActivityUtcKey}']",
            DateTimeOffset.UtcNow, 
            serializerOptions: _jsonOptions.Serializer);

        Task<AuthenticationTicket?> getTask = Json.GetAsync<AuthenticationTicket>(GetKey(key), serializerOptions: _jsonOptions.Serializer);

        // All these 'Pipelining' methods send the commands over to the server in one network operation, but differ by the below:
        // TPL: No guarantee of interleaving.
        // Batching: Guarantees no other commands through this multiplexer will be interleaved.
        // Transaction: Guarantees no other commands can be interleaved at the server level, i.e. fully atomic.
        // See: https://stackexchange.github.io/StackExchange.Redis/PipelinesMultiplexers.html
        await Task.WhenAll(setTask, getTask);

        // Task will have already been completed by this point so this await should return
        // immediately. We might want to consider using .Result here also if it's quicker?
        return await getTask;
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

    // Copied from 'AuthenticationProperties' to keep format of date items the same.
    public static DateTimeOffset? GetLastActivity(AuthenticationTicket ticket)
    {
        if (ticket.Properties.Items.TryGetValue(LastActivityUtcKey, out string? value) && DateTimeOffset.TryParseExact(
            value,
            UtcDateTimeFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTimeOffset dto))
        {
            return dto;
        }

        return null;
    }

    #region Private Methods

    private string GetKey(string? key) => string.IsNullOrWhiteSpace(key) is false && key?.StartsWith(KeyPrefix) is true
        ? key
        : KeyPrefix + key;

    // Copied from 'AuthenticationProperties' to keep format of date items the same.
    private static void SetLastActivity(AuthenticationTicket ticket, DateTimeOffset? value)
    {
        if (value.HasValue)
        {
            // TODO: Do we need to worry about thread safety here? 
            // The base implementation in 'AuthenticationProperties' doesn't seem to care, so maybe it's fine...
            ticket.Properties.Items[LastActivityUtcKey] = value.GetValueOrDefault().ToString(UtcDateTimeFormat, CultureInfo.InvariantCulture);
        }
        else
        {
            ticket.Properties.Items.Remove(LastActivityUtcKey);
        }
    }

    #endregion
}
