using AuthenticationServer.BundId;
using BuergerPortal.BundId;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BuergerPortal.Tests.Auth
{
    /// <summary>
    /// Speicher des SAML-Inspektors: nur die eigene Anmeldung (gleicher Browser und gleiches Konto), nur wenige Minuten,
    /// abgeschaltet nichts aufbewahren.
    /// </summary>
    public class BundIdInspectionStoreTests
    {
        private sealed class ManualTime(DateTimeOffset now) : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = now;
            public override DateTimeOffset GetUtcNow() => Now;
        }

        private static readonly BundIdInspection Inspection = new()
        {
            ReceivedUtc = DateTime.UtcNow,
            RequestId = "_req-0001",
            AssertionId = "_a-0001",
            Issuer = "https://bundid.example.test/saml/metadata",
            RequestedLevel = 1,
            ReachedLevel = 1,
            Checks = [],
            Attributes = [],
            ResponseXml = "<samlp:Response />",
        };

        private static (BundIdInspectionStore Store, ManualTime Time) Create(bool enabled = true)
        {
            var time = new ManualTime(new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.Zero));
            var options = Options.Create(new BundIdInspektorOptions { Enabled = enabled, MinutesVisible = 5 });
            return (new BundIdInspectionStore(options, time), time);
        }

        // Speichert für "konto-1" und liefert den Cookie, den der Browser zurückbekäme.
        private static string? SaveAndGetCookie(BundIdInspectionStore store)
        {
            var context = new DefaultHttpContext();
            context.Request.PathBase = "";
            store.Save(context.Response, "konto-1", Inspection);
            var header = context.Response.Headers.SetCookie.ToString();
            return string.IsNullOrEmpty(header) ? null : header;
        }

        private static HttpRequest RequestWithCookie(string? setCookieHeader)
        {
            var context = new DefaultHttpContext();
            if (setCookieHeader != null)
            {
                context.Request.Headers.Cookie = setCookieHeader.Split(';')[0];
            }
            return context.Request;
        }

        [Fact]
        public void Eigene_frische_Anmeldung_wird_gefunden()
        {
            var (store, _) = Create();
            var cookie = SaveAndGetCookie(store);

            Assert.Same(Inspection, store.Find(RequestWithCookie(cookie), "konto-1")?.Inspection);
        }

        [Fact]
        public void Cookie_ist_HttpOnly_Secure_und_auf_den_Inspektor_beschraenkt()
        {
            var (store, _) = Create();
            var cookie = SaveAndGetCookie(store)!;

            Assert.StartsWith(BundIdInspectionStore.CookieName + "=", cookie);
            Assert.Contains("path=/bundid/inspektor", cookie);
            Assert.Contains("secure", cookie);
            Assert.Contains("httponly", cookie);
            Assert.Contains("samesite=lax", cookie);
        }

        [Fact]
        public void Anderes_Konto_im_selben_Browser_sieht_nichts()
        {
            var (store, _) = Create();
            var cookie = SaveAndGetCookie(store);

            Assert.Null(store.Find(RequestWithCookie(cookie), "konto-2"));
            Assert.Null(store.Find(RequestWithCookie(cookie), null));
        }

        [Fact]
        public void Gleiches_Konto_in_einem_anderen_Browser_sieht_nichts()
        {
            // In der Simulation können mehrere Personen dieselbe Testperson wählen (gleiches Konto).
            var (store, _) = Create();
            SaveAndGetCookie(store);

            Assert.Null(store.Find(RequestWithCookie(null), "konto-1"));
            Assert.Null(store.Find(RequestWithCookie("bundid_inspektor=erfunden"), "konto-1"));
        }

        [Fact]
        public void Nach_der_Sichtbarkeitsdauer_ist_nichts_mehr_zu_sehen()
        {
            var (store, time) = Create();
            var cookie = SaveAndGetCookie(store);

            time.Now = time.Now.AddMinutes(4);
            Assert.NotNull(store.Find(RequestWithCookie(cookie), "konto-1"));
            time.Now = time.Now.AddMinutes(1);
            Assert.Null(store.Find(RequestWithCookie(cookie), "konto-1"));
        }

        [Fact]
        public void Abgeschaltet_wird_nichts_aufbewahrt_und_kein_Cookie_gesetzt()
        {
            var (store, _) = Create(enabled: false);

            Assert.Null(SaveAndGetCookie(store));
        }
    }
}
