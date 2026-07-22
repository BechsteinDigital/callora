using System.Text;
using System.Text.Json;
using Callora.Plugin.Communication.Domain.Accounts;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persists the polymorphic <see cref="SipAuthentication"/> as one JSON column with a <c>method</c>
/// discriminator, reconstructing the concrete type on read. Keeps the domain free of any
/// serialization concern and avoids EF's owned-type inheritance limitations — only the fields the
/// method actually uses are stored (an IP-authenticated trunk stores just its method).
/// </summary>
internal sealed class SipAuthenticationJsonConverter : ValueConverter<SipAuthentication, string>
{
    public SipAuthenticationJsonConverter()
        : base(authentication => Serialize(authentication), json => Deserialize(json))
    {
    }

    private static string Serialize(SipAuthentication authentication)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("method", authentication.Method.ToString());

            switch (authentication)
            {
                case DigestAuthentication digest:
                    writer.WriteString("username", digest.Username);
                    if (digest.AuthId is not null)
                    {
                        writer.WriteString("authId", digest.AuthId);
                    }

                    writer.WriteString("passwordSecretRef", digest.PasswordSecretRef);
                    break;

                case MutualTlsAuthentication mutualTls:
                    writer.WriteString("clientCertificateSecretRef", mutualTls.ClientCertificateSecretRef);
                    break;

                case IpAuthentication:
                    break;
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static SipAuthentication Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var method = Enum.Parse<SipAuthMethod>(root.GetProperty("method").GetString()!);

        return method switch
        {
            SipAuthMethod.Digest => new DigestAuthentication(
                root.GetProperty("username").GetString()!,
                root.TryGetProperty("authId", out var authId) ? authId.GetString() : null,
                root.GetProperty("passwordSecretRef").GetString()!),
            SipAuthMethod.MutualTls => new MutualTlsAuthentication(
                root.GetProperty("clientCertificateSecretRef").GetString()!),
            SipAuthMethod.IpAuthenticated => IpAuthentication.Instance,
            _ => throw new InvalidOperationException($"Unknown SIP auth method '{method}'.")
        };
    }
}
