using System.Text.RegularExpressions;

namespace NRedisKit.Extensions;

internal static class ConfigurationOptionsExtensions
{
    private static readonly Regex Pattern = new("^redis://(?<username>.+):(?<password>.+)@(?<host>.+):(?<port>\\d+)");

    /// <summary>
    ///     Creates an instance of <see cref="ConfigurationOptions" /> to be used
    ///     for connecting to Redis from a "CLI-style" Connection URI.
    /// </summary>
    /// <remarks>
    ///     See: https://khalidabuhakmeh.com/redis-cli-connection-uri-with-stackexchangeredis
    /// </remarks>
    /// <param name="options"></param>
    /// <param name="connectionUri">
    ///     The Connnection URI to create from. MUST be in the format of:
    ///     <c>redis://username:password@host:port</c>
    /// </param>
    /// <returns>A new instance of <see cref="ConfigurationOptions" />.</returns>
    internal static ConfigurationOptions FromCli(this ConfigurationOptions options, string connectionUri)
    {
        Match match = Pattern.Match(connectionUri);

        if (match.Success is false)
        {
            throw new ArgumentException(
                $"Cli connection string was not correct. Be sure it follows the pattern: {Pattern}",
                nameof(connectionUri));
        }

        GroupCollection values = match.Groups;

        // Host : Port
        options.EndPoints.Add($"{values["host"].Value}:{values["port"].Value}");

        // Username
        options.User = values["username"].Value;

        // Password
        options.Password = values["password"].Value;

        // TODO: What else can get out of a CLI style string? 
        // Otherwise we may want to think about setting other sensible defaults.

        return options;
    }
}
