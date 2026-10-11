using BuergerPortal.BundId;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace AuthenticationServer.BundId
{
    /// <summary>
    /// Bewahrt das Prüfprotokoll der letzten Anmeldung für den SAML-Inspektor auf: nur im Arbeitsspeicher, nur wenige
    /// Minuten und nur für den Browser, der sich angemeldet hat, und das Konto, mit dem er angemeldet ist. Der Browser
    /// bekommt dafür einen zufälligen Schlüssel als Cookie. Beides ist nötig, weil in der Simulation mehrere Personen
    /// dieselbe Testperson (= dasselbe Konto) wählen können.
    /// </summary>
    public sealed class BundIdInspectionStore(IOptions<BundIdInspektorOptions> options, TimeProvider time) : IDisposable
    {
        public const string CookieName = "bundid_inspektor";

        // Obergrenze gegen vollen Speicher (ein Eintrag ≈ 20 KB); die Anmeldung ist zudem je IP begrenzt (Rate-Limiting).
        private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 500 });

        public sealed record Entry(string UserId, BundIdInspection Inspection, DateTimeOffset VisibleUntil);

        public void Save(HttpResponse response, string userId, BundIdInspection inspection)
        {
            if (!options.Value.Enabled)
            {
                return;
            }
            var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var visibleUntil = time.GetUtcNow().AddMinutes(options.Value.MinutesVisible);
            cache.Set(key, new Entry(userId, inspection, visibleUntil),
                new MemoryCacheEntryOptions { AbsoluteExpiration = visibleUntil, Size = 1 });
            response.Cookies.Append(CookieName, key, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                // Der Aufruf kommt als normaler Link aus dem Portal (Top-Level-Navigation) – Lax genügt.
                SameSite = SameSiteMode.Lax,
                Path = response.HttpContext.Request.PathBase.Add(new PathString("/bundid/inspektor")).Value,
                Expires = visibleUntil,
                IsEssential = true,
            });
        }

        /// <summary>Protokoll der eigenen, noch sichtbaren Anmeldung dieses Browsers; sonst null.</summary>
        public Entry? Find(HttpRequest request, string? userId)
        {
            if (!options.Value.Enabled || string.IsNullOrEmpty(userId)
                || !request.Cookies.TryGetValue(CookieName, out var key) || string.IsNullOrEmpty(key))
            {
                return null;
            }
            return cache.TryGetValue(key, out Entry? entry) && entry!.UserId == userId && entry.VisibleUntil > time.GetUtcNow()
                ? entry
                : null;
        }

        public void Dispose() => cache.Dispose();
    }
}
