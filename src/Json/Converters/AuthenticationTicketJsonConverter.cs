using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace NRedisKit.Json.Converters;

// This should probably be in a completely different library as it's
// not actually coupled to the Redis implementation at all... 
/// <inheritdoc />
internal sealed class AuthenticationTicketJsonConverter : JsonConverter<AuthenticationTicket>
{
    #region Read

    /// <inheritdoc />
    public override AuthenticationTicket Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        // Start "AuthenticationTicket" object
        if (reader.TokenType is not JsonTokenType.StartObject) throw new JsonException("Invalid JSON object.");

        // Read "AuthenticationScheme" property
        string authenticationScheme = GetStringProperty(ref reader, nameof(AuthenticationTicket.AuthenticationScheme))
                                      ?? CookieAuthenticationDefaults.AuthenticationScheme;

        // Read "Principal" property
        ClaimsPrincipal principal = ReadClaimsPrincipal(ref reader);

        // Read "Properties" property
        AuthenticationProperties properties = ReadAuthenticationProperties(ref reader);

        // End of "AuthenticationTicket" object
        if (reader.Read() && reader.TokenType is JsonTokenType.EndObject)
        {
            return new AuthenticationTicket(
                principal,
                properties,
                authenticationScheme);
        }

        throw new JsonException($"Expected token type EndObject for AuthenticationTicket, but found {reader.TokenType}.");
    }

    /// <summary>
    /// Read a <see cref="ClaimsPrincipal"/> from the JSON serialized <see cref="AuthenticationTicket"/>.
    /// </summary>
    /// <param name="reader">The UTF-8 JSON reader.</param>
    /// <returns>A new <see cref="ClaimsPrincipal"/> for the stored <see cref="AuthenticationTicket"/>.</returns>
    /// <exception cref="JsonException">Throws a <see cref="JsonException"/> during deserialization problems.</exception>
    private static ClaimsPrincipal ReadClaimsPrincipal(ref Utf8JsonReader reader)
    {
        // Read "Principal" property name
        ReadPropertyName(ref reader, nameof(AuthenticationTicket.Principal));

        // Read "Principal" property value
        ReadPropertyValue(ref reader, nameof(AuthenticationTicket.Principal), JsonTokenType.StartObject);

        // Read "Identities" property name
        ReadPropertyName(ref reader, nameof(AuthenticationTicket.Principal.Identities));

        // Read "Identities" property value
        ReadPropertyValue(ref reader, nameof(AuthenticationTicket.Principal.Identities), JsonTokenType.StartArray);

        IList<ClaimsIdentity> identities = new List<ClaimsIdentity>();

        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.StartObject)
            {
                identities.Add(ReadClaimsIdentity(ref reader));
            }
            // End of "Identities" property array
            else if (reader.TokenType is JsonTokenType.EndArray) break;
        }

        // End of "Principal" property object
        if (reader.Read() && reader.TokenType is JsonTokenType.EndObject) return new ClaimsPrincipal(identities);

        throw new JsonException($"Expected token type EndObject for ClaimsPrincipal, but found {reader.TokenType}.");
    }

    /// <summary>
    /// Read a <see cref="ClaimsIdentity"/> from the JSON serialized <see cref="AuthenticationTicket"/>.
    /// </summary>
    /// <param name="reader">The UTF-8 JSON reader.</param>
    /// <returns>A new <see cref="ClaimsIdentity"/> for the stored <see cref="AuthenticationTicket"/>.</returns>
    /// <exception cref="JsonException">Throws a <see cref="JsonException"/> during deserialization problems.</exception>
    private static ClaimsIdentity ReadClaimsIdentity(ref Utf8JsonReader reader)
    {
        string? authenticationType = GetStringProperty(ref reader, nameof(ClaimsIdentity.AuthenticationType));
        string nameClaimType = GetStringProperty(ref reader, nameof(ClaimsIdentity.NameClaimType)) ?? ClaimsIdentity.DefaultNameClaimType;
        string roleClaimType = GetStringProperty(ref reader, nameof(ClaimsIdentity.RoleClaimType)) ?? ClaimsIdentity.DefaultRoleClaimType;

        IEnumerable<Claim> claims = ReadClaims(ref reader);

        ClaimsIdentity identity = new(claims, authenticationType, nameClaimType, roleClaimType);

        if (reader.Read() && reader.TokenType is JsonTokenType.EndObject) return identity;

        throw new JsonException($"Expected token type EndObject, but found {reader.TokenType}.");
    }

    /// <summary>
    /// Read an <see cref="IEnumerable{Claim}"/> from the JSON serialized <see cref="AuthenticationTicket"/>.
    /// </summary>
    /// <param name="reader">The UTF-8 JSON reader.</param>
    /// <returns>A new IEnumerable of <see cref="Claim"/>s from the stored <see cref="AuthenticationTicket"/>.</returns>
    private static IEnumerable<Claim> ReadClaims(ref Utf8JsonReader reader)
    {
        // Read "Claims" property name
        ReadPropertyName(ref reader, nameof(ClaimsIdentity.Claims));

        // Read "Claims" property value
        ReadPropertyValue(ref reader, nameof(ClaimsIdentity.Claims), JsonTokenType.StartArray);

        IList<Claim> claims = new List<Claim>();

        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.StartObject)
            {
                claims.Add(ReadClaim(ref reader));
            }
            // End of "Claims" property array
            else if (reader.TokenType is JsonTokenType.EndArray) break;
        }

        return claims;
    }

    /// <summary>
    /// Read a <see cref="Claim"/> from the JSON serialized <see cref="AuthenticationTicket"/>.
    /// </summary>
    /// <param name="reader">The UTF-8 JSON reader.</param>
    /// <returns>A new <see cref="Claim"/> from the stored <see cref="AuthenticationTicket"/>.</returns>
    /// <exception cref="JsonException">Throws a <see cref="JsonException"/> during deserialization problems.</exception>
    private static Claim ReadClaim(ref Utf8JsonReader reader)
    {
        string? issuer = ClaimsIdentity.DefaultIssuer;
        string? originalIssuer = null;
        string? claimType = null;
        string? claimValue = null;

        string claimValueType = ClaimValueTypes.String;

        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.EndObject) break;

            if (reader.TokenType is not JsonTokenType.PropertyName)
            {
                throw new JsonException($"Expected JSON token type PropertyName, but found: {reader.TokenType}.");
            }

            string? propertyName = reader.GetString();

            if (reader.Read() is false) throw new JsonException($"Cannot read JSON token for property name: {propertyName}.");

            switch (propertyName)
            {
                // TODO: The below could probably be cleaned up and unified to avoid these duplicated Exception messages.

                case nameof(Claim.Issuer):
                {
                    if (reader.TokenType is not JsonTokenType.String) throw new JsonException("Invalid JSON token for Claim.Issuer property value.");
                    issuer = reader.GetString();
                    break;
                }
                case nameof(Claim.OriginalIssuer):
                {
                    if (reader.TokenType is not JsonTokenType.String) throw new JsonException("Invalid JSON token for Claim.OriginalIssuer property value.");
                    originalIssuer = reader.GetString();
                    break;
                }
                case nameof(Claim.Type):
                {
                    if (reader.TokenType is not JsonTokenType.String) throw new JsonException("Invalid JSON token for Claim.Type property value.");
                    claimType = reader.GetString();
                    break;
                }
                case nameof(Claim.ValueType):
                {
                    if (reader.TokenType is not JsonTokenType.String) throw new JsonException("Invalid JSON token for Claim.ValueType property value.");

                    // We need to ensure that "Claim.ValueType" is serialized and stored before
                    // the "Claim.Value" so that we know how to deserialize the value correctly.
                    claimValueType = reader.GetString() ?? ClaimValueTypes.String;
                    break;
                }
                case nameof(Claim.Value):
                {
                    // May want to add further Claim Types in here... Int64 etc.
                    switch (claimValueType)
                    {
                        case ClaimValueTypes.Boolean:
                        {
                            if (reader.TokenType is not (JsonTokenType.False or JsonTokenType.True))
                                throw new JsonException("Invalid JSON token for boolean Claim.Value property value.");

                            claimValue = reader.GetBoolean().ToString();
                            break;
                        }
                        case ClaimValueTypes.Integer:
                        case ClaimValueTypes.Integer32:
                        {
                            if (reader.TokenType is not JsonTokenType.Number) throw new JsonException("Invalid JSON token for integer Claim.Value property value.");
                            claimValue = reader.GetInt32().ToString();
                            break;
                        }
                        case ClaimValueTypes.Integer64:
                        {
                            if (reader.TokenType is not JsonTokenType.Number) throw new JsonException("Invalid JSON token for integer64 Claim.Value property value.");
                            claimValue = reader.GetInt64().ToString();
                            break;
                        }
                        default:
                        {
                            if (reader.TokenType is not JsonTokenType.String) throw new JsonException("Invalid JSON token for string Claim.Value property value.");
                            claimValue = reader.GetString();
                            break;
                        }
                    }

                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(claimType) || string.IsNullOrWhiteSpace(claimValue))
            throw new JsonException("Invalid JSON. Claim contains no Type or Value property.");

        return new Claim(claimType, claimValue, claimValueType, issuer, originalIssuer);
    }

    /// <summary>
    /// Read the <see cref="AuthenticationProperties"/> from the JSON serialized <see cref="AuthenticationTicket"/>.
    /// </summary>
    /// <param name="reader">A UTF-8 JSON reader.</param>
    /// <returns>The <see cref="AuthenticationProperties"/> from the stored <see cref="AuthenticationTicket"/>.</returns>
    /// <exception cref="JsonException">Throws a <see cref="JsonException"/> during deserialization problems.</exception>
    private static AuthenticationProperties ReadAuthenticationProperties(ref Utf8JsonReader reader)
    {
        // Read "Properties" property name
        ReadPropertyName(ref reader, nameof(AuthenticationTicket.Properties));

        // Read "Properties" property value
        ReadPropertyValue(ref reader, nameof(AuthenticationTicket.Properties), JsonTokenType.StartObject);

        // Read "Items" property name
        ReadPropertyName(ref reader, nameof(AuthenticationTicket.Properties.Items));

        // Read "Items" property value
        ReadPropertyValue(ref reader, nameof(AuthenticationTicket.Properties.Items), JsonTokenType.StartObject);

        Dictionary<string, string?> items = [];

        while (reader.Read())
        {
            // End of "Items" property object
            if (reader.TokenType is JsonTokenType.EndObject) break;

            if (reader.TokenType is not JsonTokenType.PropertyName)
            {
                throw new JsonException($"Expected token type PropertyName, but found {reader.TokenType}.");
            }

            string? propertyName = reader.GetString();
            if (propertyName is null) continue;

            string? propertyValue = GetStringPropertyValue(ref reader, propertyName);
            if (propertyValue is null) continue;

            // I don't think we can ever have a duplicate
            // property name stored in th "Items" dictionary,
            // so should be safe to add this value as new.
            items.Add(propertyName, propertyValue);
        }

        // End of "Properties" property object
        if (reader.Read() && reader.TokenType is JsonTokenType.EndObject) return new AuthenticationProperties(items);

        throw new JsonException($"Expected token type EndObject, but found {reader.TokenType}.");
    }

    /// <summary>
    /// Get a string property value from the <see cref="Utf8JsonReader"/> with a specified name.
    /// </summary>
    /// <param name="reader">The UTF-8 JSON reader.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <returns>The string property value when successful, otherwise null.</returns>
    private static string? GetStringProperty(ref Utf8JsonReader reader, string propertyName)
    {
        ReadPropertyName(ref reader, propertyName);

        return GetStringPropertyValue(ref reader, propertyName);
    }

    /// <summary>
    /// Get the next <see cref="string"/> property name from the <see cref="Utf8JsonReader"/>.
    /// </summary>
    /// <param name="reader">The UTF-8 JSON reader.</param>
    /// <returns>A <see cref="string"/> when the next token was a property name, otherwise null.</returns>
    /// <exception cref="JsonException">Throws a <see cref="JsonException"/> when token was not a property name.</exception>
    private static string? GetPropertyName(ref Utf8JsonReader reader)
    {
        if (reader.Read() is false) throw new JsonException("Cannot read JSON token property name.");

        if (reader.TokenType is not JsonTokenType.PropertyName)
        {
            throw new JsonException($"Expected token type PropertyName, but found {reader.TokenType}.");
        }

        return reader.GetString();
    }

    /// <summary>
    /// Read the next <see cref="string"/> property name and confirm it matches the expected value.
    /// </summary>
    /// <param name="reader">The UTF-8 JSON reader.</param>
    /// <param name="propertyName">The expected property name to read.</param>
    /// <exception cref="JsonException">Throws when property name does not match expected value.</exception>
    private static void ReadPropertyName(ref Utf8JsonReader reader, string propertyName)
    {
        string? jsonPropertyName = GetPropertyName(ref reader);

        if (jsonPropertyName?.Equals(propertyName, StringComparison.Ordinal) is false)
        {
            throw new JsonException($"Expected JSON PropertyName: {propertyName}, but found {jsonPropertyName}.");
        }
    }

    /// <summary>
    /// Read the next <see cref="string"/> property value and confirm it matches the expected JSON token type.
    /// </summary>
    /// <param name="reader">The UTF-8 JSON reader.</param>
    /// <param name="propertyName">The expected property name value to read.</param>
    /// <param name="propertyValueType">The expected property value <see cref="JsonTokenType"/>.</param>
    /// <exception cref="JsonException">Throws when property type does not match expected value.</exception>
    private static void ReadPropertyValue(
        ref Utf8JsonReader reader,
        string propertyName,
        JsonTokenType propertyValueType)
    {
        if (reader.Read() is false) throw new JsonException($"Cannot read JSON token property value: {propertyName}.");

        if (reader.TokenType != propertyValueType)
        {
            throw new JsonException($"Expected token type {propertyValueType}, but found {reader.TokenType}.");
        }
    }

    /// <summary>
    /// Get the next <see cref="string"/> property value from the JSON reader.
    /// </summary>
    /// <param name="reader">The UTF-8 JSON reader.</param>
    /// <param name="propertyName">The <see cref="string"/> property name to read.</param>
    /// <returns>A <see cref="string"/> if reading was successful, otherwise null.</returns>
    private static string? GetStringPropertyValue(ref Utf8JsonReader reader, string propertyName)
    {
        ReadPropertyValue(ref reader, propertyName, JsonTokenType.String);

        return reader.GetString();
    }

    #endregion

    #region Write

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        AuthenticationTicket ticket,
        JsonSerializerOptions options)
    {
        // Start Authentication Ticket
        writer.WriteStartObject();

        // Authentication Scheme
        writer.WriteString(nameof(ticket.AuthenticationScheme), ticket.AuthenticationScheme);

        // Claims Principal
        WriteClaimsPrincipal(writer, ticket);

        WriteAuthenticationProperties(writer, ticket);

        // End Authentication Ticket
        writer.WriteEndObject();
    }

    /// <summary>
    /// Write the <see cref="ClaimsPrincipal"/> from the <see cref="AuthenticationTicket"/> into the <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <param name="writer">The UTF-8 JSON writer.</param>
    /// <param name="ticket">The <see cref="AuthenticationTicket"/> to write the principal from.></param>
    private static void WriteClaimsPrincipal(Utf8JsonWriter writer, AuthenticationTicket ticket)
    {
        // Start Claims Principal
        writer.WriteStartObject(nameof(ticket.Principal));

        if (ticket.Principal.Identities.Any())
        {
            // There is at least 1 ClaimsIdentity we need to serialize
            // NOTE: We do NOT need to worry about serializing Principal.Identity or
            // the Principal.Claims as both are derived at run-time from the Identities.

            // Start Identities
            writer.WriteStartArray(nameof(ticket.Principal.Identities));

            foreach (ClaimsIdentity identity in ticket.Principal.Identities)
            {
                // Write Identity
                WriteClaimsIdentity(writer, identity);
            }

            // End Identities
            writer.WriteEndArray();
        }

        // End Claims Principal
        writer.WriteEndObject();
    }

    /// <summary>
    /// Write the <see cref="ClaimsIdentity"/> into the <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <param name="writer">The UTF-8 JSON writer.</param>
    /// <param name="identity">The <see cref="ClaimsIdentity"/> to write as JSON.</param>
    private static void WriteClaimsIdentity(Utf8JsonWriter writer, ClaimsIdentity identity)
    {
        // Start Identity
        writer.WriteStartObject();

        if (identity.AuthenticationType is not null)
        {
            writer.WriteString(nameof(identity.AuthenticationType), identity.AuthenticationType);
        }

        // We do NOT need to write the identity.Name or identity.IsAuthenticated
        // as these are both also derived from the Claims at run-time.

        writer.WriteString(nameof(identity.NameClaimType), identity.NameClaimType);
        writer.WriteString(nameof(identity.RoleClaimType), identity.RoleClaimType);

        // Start Claims
        writer.WriteStartArray(nameof(identity.Claims));

        foreach (Claim claim in identity.Claims)
        {
            // Write Claim
            WriteClaim(writer, claim);
        }

        // End Claims
        writer.WriteEndArray();

        // End Identity
        writer.WriteEndObject();
    }

    /// <summary>
    /// Write the <see cref="Claim"/> into the <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <param name="writer">The UTF-8 JSON writer.</param>
    /// <param name="claim">The <see cref="Claim"/> to write as JSON.</param>
    private static void WriteClaim(Utf8JsonWriter writer, Claim claim)
    {
        // Start Claim
        writer.WriteStartObject();

        writer.WriteString(nameof(claim.Issuer), claim.Issuer);

        if (claim.OriginalIssuer.Equals(claim.Issuer, StringComparison.Ordinal) is false)
        {
            // We only need to store an 'Original Issuer' of a
            // Claim if it differs to it's current issuer.
            writer.WriteString(nameof(claim.OriginalIssuer), claim.OriginalIssuer);
        }

        writer.WriteString(nameof(claim.Type), claim.Type);

        if (claim.ValueType.Equals(ClaimValueTypes.String))
        {
            writer.WriteString(nameof(claim.Value), claim.Value);
        }
        else
        {
            // We only need to include the Claim 'Value Type'
            // when it  differs from the default string value.
            // This needs to appear before the 'Value' so we know
            // how to convert on Deserialization.
            writer.WriteString(nameof(claim.ValueType), claim.ValueType);

            // TODO: Plenty more Claim Value types that we may want to
            // implement here as and when required, e.g. double, datetime etc...
            switch (claim.ValueType)
            {
                case ClaimValueTypes.Boolean:
                    writer.WriteBoolean(nameof(claim.Value), Convert.ToBoolean(claim.Value));
                    break;

                case ClaimValueTypes.Integer:
                case ClaimValueTypes.Integer32:
                    writer.WriteNumber(nameof(claim.Value), Convert.ToInt32(claim.Value));
                    break;

                case ClaimValueTypes.Integer64:
                    writer.WriteNumber(nameof(claim.Value), Convert.ToInt64(claim.Value));
                    break;
            }
        }

        // End Claim
        writer.WriteEndObject();
    }

    /// <summary>
    /// Write the <see cref="AuthenticationProperties"/> property of the <see cref="AuthenticationTicket"/>
    /// into the <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <param name="writer">The UTF-8 JSON writer.</param>
    /// <param name="ticket">The <see cref="AuthenticationTicket"/> to write the <see cref="AuthenticationProperties"/> from.</param>
    private static void WriteAuthenticationProperties(Utf8JsonWriter writer, AuthenticationTicket ticket)
    {
        // Start Authentication Properties
        writer.WriteStartObject(nameof(ticket.Properties));

        // Start Item Dictionary
        writer.WriteStartObject(nameof(ticket.Properties.Items));

        foreach (KeyValuePair<string, string> item in ticket.Properties.Items)
        {
            // We may want to check 'HandleNull' here first in case
            // we are expecting null values to be included.
            if (item.Key is null || item.Value is null) continue;

            writer.WriteString(item.Key, item.Value);
        }

        // End Item Dictionary
        writer.WriteEndObject();

        // End Authentication Properties
        writer.WriteEndObject();
    }

    #endregion
}
