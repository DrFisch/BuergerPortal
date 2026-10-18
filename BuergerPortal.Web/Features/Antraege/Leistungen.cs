using BuergerPortal.BundId;
using System.Globalization;

namespace BuergerPortal.Web.Features.Antraege
{
    /// <summary>
    /// Eine Leistung, die man im Portal beantragen kann. <see cref="Typ"/> entspricht <c>AntragTyp</c> der API.
    /// Texte (Titel, Kurztext, Unterlagen, Gebühr) stehen in Resources/Features/Antraege/Leistungen(.en).resx unter
    /// „{Schluessel}_Titel“, „{Schluessel}_Kurz“, „{Schluessel}_Unterlagen“ (durch | getrennt) und „{Schluessel}_Gebuehr“.
    /// </summary>
    public sealed record LeistungInfo(int Typ, string Schluessel, string Icon, string Farbe, int Niveau, int DauerMinuten,
        string Controller, string Action, string DetailAction);

    /// <summary>
    /// Leistungskatalog der Anträge (2026-10-18): Übersicht, Assistenten und Detailseiten lesen Symbol, nötiges
    /// BundID-Vertrauensniveau, Dauer und Gebühr von hier. Das Niveau ist dasselbe, das die API beim Einreichen prüft
    /// (Reisepass: substanziell, TrustLevelPolicies) – die Übersicht sagt es also vorher, statt dass erst der Step-up
    /// überrascht. Nicht statisch, weil die Texte über IStringLocalizer&lt;Leistungen&gt; kommen.
    /// </summary>
    public sealed class Leistungen
    {
        private Leistungen()
        {
        }

        public const int Reisepass = 1;
        public const int Sperrmuell = 2;

        public static readonly IReadOnlyList<LeistungInfo> Alle =
        [
            new(Reisepass, "Reisepass", "bi-passport", "primary", TrustLevel.Substantial, 5,
                "ReisepassAntraege", "NeuerReisepass", "Antrag"),
            new(Sperrmuell, "Sperrmuell", "bi-truck", "success", TrustLevel.Normal, 3,
                "SperrmuellAntraege", "NeuerSperrmuell", "SperrmuellAntrag"),
        ];

        public static LeistungInfo? Von(int typ) => Alle.FirstOrDefault(l => l.Typ == typ);

        /// <summary>Reicht das Niveau der Anmeldung für die Leistung? (sonst Step-up beim Einreichen)</summary>
        public static bool NiveauReicht(int aktuell, int noetig) => aktuell >= noetig;

        /// <summary>Alter in vollendeten Jahren am Stichtag (Geburtstag am 29.02. zählt in Nicht-Schaltjahren am 28.02.).</summary>
        public static int Alter(DateOnly geburt, DateOnly stichtag)
        {
            var alter = stichtag.Year - geburt.Year;
            var geburtstag = geburt.AddYears(alter);
            return stichtag < geburtstag ? alter - 1 : alter;
        }

        /// <summary>
        /// Gebühr für einen Reisepass nach § 27 PassV: 37,50 € vor, 70 € ab Vollendung des 24. Lebensjahres, im
        /// Expressverfahren zusätzlich 32 € (abgerufen 2026-10-18; 48-Seiten-Pass hier nicht angeboten).
        /// </summary>
        public static decimal ReisepassGebuehr(DateOnly geburt, DateOnly stichtag, bool express) =>
            (Alter(geburt, stichtag) >= 24 ? 70m : 37.50m) + (express ? 32m : 0m);

        /// <summary>Betrag in Euro in der Schreibweise der Sprache („70,00 €“ bzw. „€70.00“).</summary>
        public static string Euro(decimal betrag, CultureInfo kultur)
        {
            var format = (NumberFormatInfo)kultur.NumberFormat.Clone();
            format.CurrencySymbol = "€";
            return betrag.ToString("C", format);
        }

        /// <summary>Name des Niveaus, wie die BundID es nennt (normal, substanziell, hoch).</summary>
        public static string NiveauName(int niveau) => TrustLevel.Describe(niveau);
    }
}
