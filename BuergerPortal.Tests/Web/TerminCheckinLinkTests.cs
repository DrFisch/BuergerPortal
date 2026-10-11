using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.DataProtection;

namespace BuergerPortal.Tests.Web
{
    /// <summary>Kennung im QR-Code zum Einchecken: verschlüsselt, signiert, einen Tag nach Terminende abgelaufen.</summary>
    public class TerminCheckinLinkTests
    {
        private sealed class ManualTime(DateTimeOffset now) : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = now;
            public override DateTimeOffset GetUtcNow() => Now;
        }

        [Fact]
        public void Kennung_liefert_die_Termin_ID_zurueck()
        {
            var link = new TerminCheckinLink(new EphemeralDataProtectionProvider(), TimeProvider.System);
            var id = Guid.NewGuid();

            Assert.Equal(id, link.Read(link.Create(id, DateTime.UtcNow.AddDays(3))));
        }

        [Fact]
        public void Veraenderte_oder_fremde_Kennung_ist_wertlos()
        {
            var provider = new EphemeralDataProtectionProvider();
            var link = new TerminCheckinLink(provider, TimeProvider.System);
            var token = link.Create(Guid.NewGuid(), DateTime.UtcNow.AddDays(3));

            Assert.Null(link.Read(token[..^2] + (token[^2] == 'A' ? "BB" : "AA")));
            Assert.Null(link.Read(Guid.NewGuid().ToString("N")));
            Assert.Null(link.Read(""));
            // Mit anderem Zweck geschützt (z. B. ein Sitzungs-Cookie) – nicht als Einchecklink verwendbar.
            Assert.Null(link.Read(provider.CreateProtector("Anderer.Zweck").Protect(Guid.NewGuid().ToString("N"))));
        }

        [Fact]
        public void Kennung_laeuft_einen_Tag_nach_Terminende_ab()
        {
            var time = new ManualTime(DateTimeOffset.UtcNow);
            var link = new TerminCheckinLink(new EphemeralDataProtectionProvider(), time);
            var id = Guid.NewGuid();
            var token = link.Create(id, time.Now.UtcDateTime.AddHours(2));

            time.Now = time.Now.AddHours(25);
            Assert.Equal(id, link.Read(token));
            time.Now = time.Now.AddHours(2);
            Assert.Null(link.Read(token));
        }
    }
}
