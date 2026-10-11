using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace BuergerPortal.Web.Services
{
    /// <summary>
    /// Kennung für den QR-Code zum Einchecken im Bürgeramt: die Termin-ID, verschlüsselt und signiert (Data Protection),
    /// gültig bis einen Tag nach Terminende. Wer den QR-Code scannt, sieht nur Leistung, Ort, Zeit und Status – ohne
    /// Anmeldung und ohne Personendaten; eine geratene oder veränderte Kennung ist wertlos.
    /// </summary>
    public sealed class TerminCheckinLink(IDataProtectionProvider dataProtection, TimeProvider time)
    {
        private readonly ITimeLimitedDataProtector protector =
            dataProtection.CreateProtector("Termine.Checkin").ToTimeLimitedDataProtector();

        public string Create(Guid appointmentId, DateTime endUtc)
        {
            var validUntil = new DateTimeOffset(DateTime.SpecifyKind(endUtc, DateTimeKind.Utc)).AddDays(1);
            // Mindestens eine Stunde gültig, falls der Termin gerade vorbei ist.
            var earliest = time.GetUtcNow().AddHours(1);
            return protector.Protect(appointmentId.ToString("N"), validUntil > earliest ? validUntil : earliest);
        }

        /// <summary>Termin-ID aus der Kennung; null bei falscher, veränderter oder abgelaufener Kennung.</summary>
        public Guid? Read(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }
            try
            {
                return Guid.TryParseExact(protector.Unprotect(token, out var expiration), "N", out var id)
                       && expiration > time.GetUtcNow()
                    ? id
                    : null;
            }
            catch (CryptographicException)
            {
                return null;
            }
        }
    }
}
