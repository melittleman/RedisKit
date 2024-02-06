namespace RedisKit.DependencyInjection.Options;

public sealed record RedisConnectionOptions
{
    /// <summary>
    ///     The name of the connected client that this application will
    ///     appear as when using the Redis CLI to execute 'CLIENT LIST'.
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    ///     Whether or not the use the dedicated 'SUBSCRIBE' channel
    ///     within the Redis server. This will use up a Client connection
    ///     so should be left as false if not required.
    /// </summary>
    public bool UseSubscriber { get; set; } = false;

    #region Connection Methods

    //*** Option 1. ***//
    // Use appsettings or Environment Variables to provide the raw Connection String.

    /// <summary>
    ///     <para>
    ///         Use a "Connection String" style appraoch to connect to Redis.
    ///     </para>
    ///     <para>
    ///         This can either follow the <c>StackExchange.Redis</c> format, which
    ///         contains all the information required to connect and allows
    ///         updating parameters dynamically as requirements change.
    ///     </para>
    ///     <para>
    ///         Example: <c>redis0:6379,redis1:6380,allowAdmin=true,ssl=false,etc...</c>
    ///     </para>
    ///     <para>
    ///         Or the Redis CLI style which MUST begin with the <c>redis://</c> protocol but could
    ///         be more flexible when using a shared secret between different application stacks.
    ///     </para>
    ///     <para>
    ///         Example: <c>redis://_:password@redis.com:1234/0</c>
    ///         <para>
    ///             NOTE: Passing in configuration arguments with this approach is NOT supported.
    ///         </para>
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     See: https://stackexchange.github.io/StackExchange.Redis/Configuration.html#basic-configuration-strings
    /// </remarks>
    public string? ConnectionString { get; set; }

    //*** Option 2. ***//
    // Provide an AWS Secrets Manager ARN that will contain the Redis connection credentials.

    /// <summary>
    ///     <para>
    ///         Connect to AWS Secrets Manager to retrieve
    ///         the "Connection String" credentials.
    ///     </para>
    ///     <para>
    ///         This MUST begin with <c>arn:aws:secretsmanager:</c>
    ///     </para>
    ///     <para>
    ///         Usefule to avoid the Username or Password being available in
    ///         plain text within appsetting.json or Environment Variables.
    ///     </para>
    /// </summary>
    public string? SecretsArn { get; set; }

    //*** Option 3. ***//
    // Explicitly set the Conifguration Options to be used for the Redis connection.

    /// <summary>
    ///     The Hostname and Port number of the Redis server to connect to.
    /// </summary>
    /// <remarks>
    ///     Example: localhost:6379 or cloud.redislabs.com:13546
    /// </remarks>
    public string HostnameAndPort { get; set; } = "localhost:6379";

    /// <summary>
    ///     The Username to use for the connection.
    /// </summary>
    /// <remarks>
    ///     Defaults to "default" if not provided.
    /// </remarks>
    public string Username { get; set; } = "default";

    /// <summary>
    ///     The Password to use for the connection.
    /// </summary>
    /// <remarks>
    ///     ***NOT RECOMMENDED*** due to the fact this is plain text
    ///     and storing anywhere but local appsettings or Environment
    ///     Variables is quite obviously a bad idea!
    /// </remarks>
    public string? Password { get; set; }

    /// <summary>
    ///     Whether or not to use an SSL / TLS encryted connection
    ///     to the Redis server.
    /// </summary>
    /// <remarks>
    ///     Defaults to false as this requires a certificate for
    ///     encryption which has not yet been implemented.
    /// </remarks>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    ///     Whether or not to allow Administrative operations
    ///     on the Redis server.
    /// </summary>
    /// <remarks>
    ///     ***NOT RECOMMENDED*** due to the fact that this could
    ///     result in data loss if used incorrectly.
    ///     Defaults to false.
    /// </remarks>
    public bool AllowAdmin { get; set; } = false;

    #endregion
}
