using BuergerPortal.Web.Services;
using System.Text;

namespace BuergerPortal.Tests.Web
{
    /// <summary>Kalenderdatei nach RFC 5545: Pflichtfelder, UTC-Zeiten, CRLF, Maskierung, Falten nach 75 Byte, Erinnerung.</summary>
    public class IcsCalendarTests
    {
        private static readonly IcsEvent Termin = new(
            "6f9d2c1e-0000-4000-8000-000000000001@bpsimulation.example",
            new DateTime(2026, 10, 13, 9, 15, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 13, 9, 30, 0, DateTimeKind.Utc),
            "Termin im Bürgeramt: Reisepass",
            "Bürgeramt Mitte",
            "Gebucht über das BürgerPortal; bitte pünktlich, Nummer am Eingang ziehen.\nDetails im Portal.",
            "https://bpsimulation.example/Termine/Details/6f9d2c1e-0000-4000-8000-000000000001",
            TimeSpan.FromHours(1));

        private static string Create() => IcsCalendar.Create(Termin, new DateTime(2026, 10, 11, 8, 0, 0, DateTimeKind.Utc));

        // Gefaltete Zeilen wieder zusammensetzen (RFC 5545 §3.1: CRLF gefolgt von einem Leerzeichen entfällt).
        private static string[] Unfolded(string ics) => ics.Replace("\r\n ", "").Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        [Fact]
        public void Enthaelt_einen_Termin_mit_UTC_Zeiten_und_Pflichtfeldern()
        {
            var lines = Unfolded(Create());

            Assert.Equal("BEGIN:VCALENDAR", lines[0]);
            Assert.Equal("END:VCALENDAR", lines[^1]);
            Assert.Contains("VERSION:2.0", lines);
            Assert.Contains("UID:6f9d2c1e-0000-4000-8000-000000000001@bpsimulation.example", lines);
            Assert.Contains("DTSTAMP:20261011T080000Z", lines);
            Assert.Contains("DTSTART:20261013T091500Z", lines);
            Assert.Contains("DTEND:20261013T093000Z", lines);
            Assert.Contains("SUMMARY:Termin im Bürgeramt: Reisepass", lines);
            Assert.Contains("LOCATION:Bürgeramt Mitte", lines);
            Assert.Single(lines, l => l == "BEGIN:VEVENT");
        }

        [Fact]
        public void Zeilen_enden_mit_CRLF_und_sind_hoechstens_75_Byte_lang()
        {
            var ics = Create();

            Assert.EndsWith("\r\n", ics);
            Assert.DoesNotContain("\n", ics.Replace("\r\n", ""));
            Assert.All(ics.Split("\r\n"), line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, line));
        }

        [Fact]
        public void Texte_werden_maskiert()
        {
            var description = Unfolded(Create()).First(l => l.StartsWith("DESCRIPTION:"));   // die zweite gehört zur Erinnerung

            Assert.Equal("DESCRIPTION:Gebucht über das BürgerPortal\\; bitte pünktlich\\, Nummer am Eingang ziehen.\\nDetails im Portal.", description);
        }

        [Fact]
        public void Falten_zerteilt_keine_Umlaute()
        {
            var folded = IcsCalendar.Fold("SUMMARY:" + new string('ä', 80));

            Assert.All(folded.Split("\r\n"), line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75));
            Assert.Equal("SUMMARY:" + new string('ä', 80), folded.Replace("\r\n ", ""));
        }

        [Fact]
        public void Erinnerung_eine_Stunde_vorher()
        {
            var lines = Unfolded(Create());

            Assert.Contains("BEGIN:VALARM", lines);
            Assert.Contains("TRIGGER:-PT60M", lines);
        }
    }
}
