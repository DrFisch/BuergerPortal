using BuergerPortal.Api.Push;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace BuergerPortal.Tests.Api
{
    /// <summary>
    /// Web-Push-Verschlüsselung gegen das Beispiel aus RFC 8291 (Abschnitt 5 und Anhang A): gleiche Eingaben (Schlüssel,
    /// Auth-Geheimnis, Salt) müssen byte-genau dieselbe Nachricht ergeben. Dazu ein Rundlauf mit zufälligem Schlüssel.
    /// </summary>
    public class WebPushEncryptionTests
    {
        private static byte[] B(string base64Url) => Base64Url.DecodeFromChars(base64Url.Replace(" ", ""));

        // Werte aus RFC 8291, Anhang A
        private const string Plaintext = "When I grow up, I want to be a watermelon";
        private static readonly byte[] AsPublic = B("BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8");
        private static readonly byte[] AsPrivate = B("yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw");
        private static readonly byte[] UaPublic = B("BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4");
        private static readonly byte[] UaPrivate = B("q1dXpw3UpT5VOmu_cf_v6ih07Aems3njxI-JWgLcM94");
        private static readonly byte[] AuthSecret = B("BTBZMqHH6r4Tts7J_aSIgg");
        private static readonly byte[] Salt = B("DGv6ra1nlYgDCS1FRnbzlw");

        // Ergebnis aus RFC 8291, Abschnitt 5 (Kopf 86 Byte + Chiffrat)
        private const string Expected =
            "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27ml" +
            "mlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPT" +
            "pK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN";

        [Fact]
        public void Ergebnis_entspricht_dem_Beispiel_aus_RFC_8291()
        {
            using var serverKey = ECDiffieHellman.Create(WebPushEncryption.ParametersFromUncompressed(AsPublic, AsPrivate));

            var body = WebPushEncryption.Encrypt(Encoding.ASCII.GetBytes(Plaintext), UaPublic, AuthSecret, serverKey, Salt);

            Assert.Equal(Expected, Base64Url.EncodeToString(body));
        }

        [Fact]
        public void Browser_kann_eine_Nachricht_mit_zufaelligem_Schluessel_entschluesseln()
        {
            var body = WebPushEncryption.Encrypt(Encoding.UTF8.GetBytes("Neue Nachricht im BundID-Postfach"), UaPublic, AuthSecret);

            Assert.Equal("Neue Nachricht im BundID-Postfach", Encoding.UTF8.GetString(DecryptAsBrowser(body)));
        }

        /// <summary>Entschlüsselung wie im Browser (RFC 8291 aus Sicht des User Agents) – nur für den Test.</summary>
        private static byte[] DecryptAsBrowser(byte[] body)
        {
            var salt = body[..16];
            var keyIdLength = body[20];
            var serverPublic = body[21..(21 + keyIdLength)];
            var ciphertext = body[(21 + keyIdLength)..^16];
            var tag = body[^16..];

            using var ua = ECDiffieHellman.Create(WebPushEncryption.ParametersFromUncompressed(UaPublic, UaPrivate));
            using var server = ECDiffieHellman.Create(WebPushEncryption.ParametersFromUncompressed(serverPublic));
            var ecdh = ua.DeriveRawSecretAgreement(server.PublicKey);
            var keyInfo = Encoding.ASCII.GetBytes("WebPush: info\0").Concat(UaPublic).Concat(serverPublic).ToArray();
            var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, ecdh, 32, AuthSecret, keyInfo);
            var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
            var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));
            var padded = new byte[ciphertext.Length];
            using (var aes = new AesGcm(cek, 16))
            {
                aes.Decrypt(nonce, ciphertext, tag, padded);
            }
            Assert.Equal(0x02, padded[^1]);
            return padded[..^1];
        }
    }
}
