using BuergerPortal.BundId;
using BuergerPortal.Web.Features.Antraege;
using System.Globalization;
using System.Xml.Linq;

namespace BuergerPortal.Tests.Web
{
    /// <summary>Leistungskatalog der Anträge: Gebühren nach § 27 PassV, Alter, Niveau und vollständige Texte.</summary>
    public class LeistungenTests
    {
        [Theory]
        // unter 24: 37,50 €, ab dem 24. Geburtstag: 70 € – Stichtag genau am Geburtstag zählt schon als 24
        [InlineData("2002-10-19", "2026-10-18", false, 37.50)]
        [InlineData("2002-10-18", "2026-10-18", false, 70)]
        [InlineData("1969-02-11", "2026-10-18", false, 70)]
        [InlineData("1969-02-11", "2026-10-18", true, 102)]
        [InlineData("2010-05-01", "2026-10-18", true, 69.50)]
        public void Reisepassgebuehr_nach_Alter_und_Express(string geburt, string stichtag, bool express, decimal erwartet)
        {
            Assert.Equal(erwartet, Leistungen.ReisepassGebuehr(DateOnly.Parse(geburt), DateOnly.Parse(stichtag), express));
        }

        [Fact]
        public void Alter_am_Geburtstag_und_am_29_Februar()
        {
            Assert.Equal(23, Leistungen.Alter(new DateOnly(2002, 10, 19), new DateOnly(2026, 10, 18)));
            Assert.Equal(24, Leistungen.Alter(new DateOnly(2002, 10, 18), new DateOnly(2026, 10, 18)));
            // 29.02.2004: in 2027 (kein Schaltjahr) zählt der 28.02. als Geburtstag
            Assert.Equal(22, Leistungen.Alter(new DateOnly(2004, 2, 29), new DateOnly(2027, 2, 27)));
            Assert.Equal(23, Leistungen.Alter(new DateOnly(2004, 2, 29), new DateOnly(2027, 2, 28)));
        }

        [Fact]
        public void Niveau_der_Leistungen_wie_die_Pruefung_der_API()
        {
            // Reisepass: Einreichen erst ab "substanziell" (API: TrustLevelPolicies.Substantial), Sperrmüll ab "normal"
            Assert.Equal(TrustLevel.Substantial, Leistungen.Von(Leistungen.Reisepass)!.Niveau);
            Assert.Equal(TrustLevel.Normal, Leistungen.Von(Leistungen.Sperrmuell)!.Niveau);
            Assert.False(Leistungen.NiveauReicht(TrustLevel.Normal, TrustLevel.Substantial));
            Assert.True(Leistungen.NiveauReicht(TrustLevel.High, TrustLevel.Substantial));
            Assert.Null(Leistungen.Von(99));
        }

        [Fact]
        public void Euro_in_der_Schreibweise_der_Sprache()
        {
            Assert.Equal("70,00 €", Leistungen.Euro(70m, CultureInfo.GetCultureInfo("de-DE")).Replace(' ', ' '));
            Assert.Equal("€37.50", Leistungen.Euro(37.50m, CultureInfo.GetCultureInfo("en-US")));
        }

        [Theory]
        [InlineData("Leistungen.resx")]
        [InlineData("Leistungen.en.resx")]
        public void Jede_Leistung_hat_alle_Texte(string datei)
        {
            var pfad = Path.Combine(RepoRoot(), "BuergerPortal.Web", "Resources", "Features", "Antraege", datei);
            var schluessel = XDocument.Load(pfad).Root!.Elements("data").Select(d => (string)d.Attribute("name")!).ToHashSet();
            foreach (var l in Leistungen.Alle)
            {
                foreach (var teil in new[] { "Titel", "Kurz", "Unterlagen", "Gebuehr" })
                {
                    Assert.Contains($"{l.Schluessel}_{teil}", schluessel);
                }
                Assert.Contains($"Niveau_{l.Niveau}", schluessel);
            }
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BuergerPortal.sln")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new InvalidOperationException("BuergerPortal.sln nicht gefunden");
        }
    }
}
