using BuergerPortal.Api.Push;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BuergerPortal.Tests.Api
{
    /// <summary>VAPID (RFC 8292): Header-Format, Ansprüche im JWT und Signatur, die mit dem öffentlichen Schlüssel prüfbar ist.</summary>
    public class VapidTests
    {
        [Fact]
        public void Header_enthaelt_ein_pruefbares_JWT_und_den_oeffentlichen_Schluessel()
        {
            var (publicKey, privateKey) = Vapid.GenerateKeys();
            var now = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

            var value = Vapid.AuthorizationHeader(new Uri("https://push.example.net/push/abc123"), "mailto:betrieb@example.org",
                publicKey, privateKey, now);

            Assert.StartsWith("vapid t=", value);
            var parts = value["vapid t=".Length..].Split(", k=");
            Assert.Equal(Base64Url.EncodeToString(publicKey), parts[1]);
            var jwt = parts[0].Split('.');
            Assert.Equal(3, jwt.Length);

            var header = JsonDocument.Parse(Base64Url.DecodeFromChars(jwt[0])).RootElement;
            Assert.Equal("ES256", header.GetProperty("alg").GetString());
            var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(jwt[1])).RootElement;
            Assert.Equal("https://push.example.net", claims.GetProperty("aud").GetString());
            Assert.Equal("mailto:betrieb@example.org", claims.GetProperty("sub").GetString());
            var exp = DateTimeOffset.FromUnixTimeSeconds(claims.GetProperty("exp").GetInt64());
            Assert.True(exp > now && exp <= now.AddHours(24), "exp muss in der Zukunft und höchstens 24 h entfernt liegen");

            using var verifier = ECDsa.Create(WebPushEncryption.ParametersFromUncompressed(publicKey));
            Assert.True(verifier.VerifyData(Encoding.ASCII.GetBytes($"{jwt[0]}.{jwt[1]}"), Base64Url.DecodeFromChars(jwt[2]),
                HashAlgorithmName.SHA256), "Signatur muss mit dem öffentlichen Schlüssel gültig sein");
        }

        [Fact]
        public void Andere_Schluessel_ergeben_keine_gueltige_Signatur()
        {
            var (publicKey, privateKey) = Vapid.GenerateKeys();
            var (fremderPublicKey, _) = Vapid.GenerateKeys();
            var jwt = Vapid.AuthorizationHeader(new Uri("https://push.example.net/x"), "mailto:a@example.org", publicKey, privateKey,
                DateTimeOffset.UtcNow)["vapid t=".Length..].Split(", k=")[0].Split('.');

            using var verifier = ECDsa.Create(WebPushEncryption.ParametersFromUncompressed(fremderPublicKey));
            Assert.False(verifier.VerifyData(Encoding.ASCII.GetBytes($"{jwt[0]}.{jwt[1]}"), Base64Url.DecodeFromChars(jwt[2]),
                HashAlgorithmName.SHA256));
        }
    }
}
