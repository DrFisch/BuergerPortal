using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace BuergerPortal.Api.Push
{
    /// <summary>
    /// Verschlüsselt eine Push-Nachricht für genau einen Browser nach RFC 8291 („Message Encryption for Web Push“) mit
    /// der Inhaltskodierung aes128gcm (RFC 8188). Der Push-Dienst (z. B. von Google, Apple, Mozilla) transportiert die
    /// Nachricht nur – lesen kann sie allein der Browser, der das Abo angelegt hat.
    /// <para>
    /// Ablauf: Einmal-Schlüsselpaar des Servers (ECDH P-256) → gemeinsames Geheimnis mit dem öffentlichen Schlüssel des
    /// Browsers (p256dh) → mit dessen Auth-Geheimnis und einem zufälligen Salt per HKDF zu Schlüssel (16 Byte) und
    /// Nonce (12 Byte) → AES-128-GCM. Vor dem Chiffrat steht ein Kopf aus Salt, Datensatzgröße und öffentlichem
    /// Server-Schlüssel, damit der Browser dieselben Werte ableiten kann.
    /// </para>
    /// </summary>
    public static class WebPushEncryption
    {
        public const int RecordSize = 4096;
        private const int TagLength = 16;

        /// <summary>Verschlüsseln mit neuem Einmal-Schlüssel und zufälligem Salt (Normalfall).</summary>
        public static byte[] Encrypt(byte[] plaintext, byte[] userAgentPublicKey, byte[] authSecret)
        {
            using var serverKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            return Encrypt(plaintext, userAgentPublicKey, authSecret, serverKey, RandomNumberGenerator.GetBytes(16));
        }

        /// <summary>Verschlüsseln mit vorgegebenem Server-Schlüssel und Salt (für den Test mit den Werten aus RFC 8291).</summary>
        public static byte[] Encrypt(byte[] plaintext, byte[] userAgentPublicKey, byte[] authSecret, ECDiffieHellman serverKey, byte[] salt)
        {
            if (plaintext.Length > RecordSize - TagLength - 1)
            {
                throw new ArgumentException("Push-Nachricht zu groß für einen Datensatz.", nameof(plaintext));
            }

            var serverPublicKey = ExportPublicKey(serverKey);
            using var userAgentKey = ImportPublicKey(userAgentPublicKey);
            var ecdhSecret = serverKey.DeriveRawSecretAgreement(userAgentKey.PublicKey);

            // IKM = HKDF(salt = auth_secret, IKM = ecdh_secret, info = "WebPush: info" || 0x00 || ua_public || as_public)
            var keyInfo = Concat(Encoding.ASCII.GetBytes("WebPush: info\0"), userAgentPublicKey, serverPublicKey);
            var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, ecdhSecret, 32, authSecret, keyInfo);
            var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
            var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

            // ein einziger (letzter) Datensatz: Klartext || 0x02 (Trennzeichen „letzter Datensatz“, keine Auffüllung)
            var padded = new byte[plaintext.Length + 1];
            plaintext.CopyTo(padded, 0);
            padded[^1] = 0x02;
            var ciphertext = new byte[padded.Length];
            var tag = new byte[TagLength];
            using (var aes = new AesGcm(cek, TagLength))
            {
                aes.Encrypt(nonce, padded, ciphertext, tag);
            }

            // Kopf: salt (16) || rs (uint32, big endian) || idlen (1) || keyid = öffentlicher Server-Schlüssel (65)
            var header = new byte[16 + 4 + 1 + serverPublicKey.Length];
            salt.CopyTo(header, 0);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16, 4), RecordSize);
            header[20] = (byte)serverPublicKey.Length;
            serverPublicKey.CopyTo(header, 21);
            return Concat(header, ciphertext, tag);
        }

        /// <summary>Öffentlicher Schlüssel unkomprimiert (0x04 || X || Y, 65 Byte) – so liefert ihn der Browser (p256dh).</summary>
        public static byte[] ExportPublicKey(ECAlgorithm key)
        {
            var q = key.ExportParameters(false).Q;
            return Concat([0x04], q.X!, q.Y!);
        }

        public static ECParameters ParametersFromUncompressed(byte[] publicKey, byte[]? privateKey = null)
        {
            if (publicKey.Length != 65 || publicKey[0] != 0x04)
            {
                throw new ArgumentException("Erwartet: unkomprimierter P-256-Schlüssel (65 Byte).", nameof(publicKey));
            }
            return new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..65] },
                D = privateKey,
            };
        }

        private static ECDiffieHellman ImportPublicKey(byte[] publicKey) => ECDiffieHellman.Create(ParametersFromUncompressed(publicKey));

        private static byte[] Concat(params byte[][] parts)
        {
            var result = new byte[parts.Sum(p => p.Length)];
            var offset = 0;
            foreach (var part in parts)
            {
                part.CopyTo(result, offset);
                offset += part.Length;
            }
            return result;
        }
    }
}
