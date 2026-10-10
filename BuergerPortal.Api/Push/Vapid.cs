using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BuergerPortal.Api.Push
{
    /// <summary>
    /// VAPID nach RFC 8292 („Voluntary Application Server Identification for Web Push“): Der Server weist sich beim
    /// Push-Dienst mit einem kurzlebigen JWT (ES256) aus, signiert mit seinem privaten Schlüssel. Den öffentlichen
    /// Schlüssel hat der Browser beim Anlegen des Abos bekommen (applicationServerKey) – der Push-Dienst nimmt nur
    /// Nachrichten an, die mit genau diesem Schlüsselpaar signiert sind. Fremde können also keine Nachrichten an die
    /// Abos des Portals schicken, selbst wenn sie die Adresse eines Abos kennen.
    /// </summary>
    public static class Vapid
    {
        /// <summary>Gültigkeit des JWT (RFC 8292: höchstens 24 Stunden).</summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

        /// <summary>
        /// Wert des Headers „Authorization“: <c>vapid t=&lt;JWT&gt;, k=&lt;öffentlicher Schlüssel&gt;</c>.
        /// <paramref name="endpoint"/> = Adresse des Abos beim Push-Dienst (daraus „aud“), <paramref name="subject"/> =
        /// Kontakt des Betreibers („mailto:…“ oder „https://…“).
        /// </summary>
        public static string AuthorizationHeader(Uri endpoint, string subject, byte[] publicKey, byte[] privateKey, DateTimeOffset now)
        {
            var header = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("{\"typ\":\"JWT\",\"alg\":\"ES256\"}"));
            var claims = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new
            {
                aud = endpoint.GetLeftPart(UriPartial.Authority),
                exp = now.Add(Lifetime).ToUnixTimeSeconds(),
                sub = subject,
            }));
            var signingInput = $"{header}.{claims}";
            using var ecdsa = ECDsa.Create(WebPushEncryption.ParametersFromUncompressed(publicKey, privateKey));
            // ES256 = ECDSA P-256 mit SHA-256, Signatur als r || s (IEEE P1363) – das liefert .NET standardmäßig
            var signature = ecdsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256);
            return $"vapid t={signingInput}.{Base64Url.EncodeToString(signature)}, k={Base64Url.EncodeToString(publicKey)}";
        }

        /// <summary>Neues Schlüsselpaar (öffentlich unkomprimiert 65 Byte, privat 32 Byte) – z. B. für die Entwicklung.</summary>
        public static (byte[] PublicKey, byte[] PrivateKey) GenerateKeys()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            return (WebPushEncryption.ExportPublicKey(key), key.ExportParameters(true).D!);
        }
    }
}
