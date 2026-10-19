using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Domain.Antrag.Enums;
using System.Globalization;

namespace BuergerPortal.Application.Antraege.AntragMeldebescheinigung
{
    /// <summary>
    /// Inhalt der (simulierten) Meldebescheinigung als Klartext für das BundID-Postfach, das keine Anhänge kennt.
    /// Einfach: Name und Anschrift; erweitert: zusätzlich Geburtsdatum, Geburtsort und Geburtsname. Ausstellungsdatum
    /// und Behörde setzt die Statusmeldung dazu.
    /// </summary>
    public static class MeldebescheinigungDokument
    {
        public static string Text(MeldebescheinigungAntrag a)
        {
            var zeilen = new List<string>
            {
                a.Art == MeldebescheinigungArt.Erweitert ? "ERWEITERTE MELDEBESCHEINIGUNG" : "EINFACHE MELDEBESCHEINIGUNG",
                "MUSTER – Simulation des Studienprojekts, keine amtliche Bescheinigung",
                string.Empty,
                $"Familienname: {a.Name.Nachname}",
                $"Vornamen: {a.Name.Vorname}",
            };
            if (a.Art == MeldebescheinigungArt.Erweitert)
            {
                if (!string.IsNullOrWhiteSpace(a.Geburtsname)) zeilen.Add($"Geburtsname: {a.Geburtsname}");
                zeilen.Add($"Geburtsdatum: {a.Birth.Geburtsdatum.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}");
                if (!string.IsNullOrWhiteSpace(a.Geburtsort)) zeilen.Add($"Geburtsort: {a.Geburtsort}");
            }
            zeilen.Add($"Anschrift (Hauptwohnung): {a.Addresse.Strasse}, {a.Addresse.PLZ} {a.Addresse.Ort}");
            if (!string.IsNullOrWhiteSpace(a.Zweck)) zeilen.Add($"Zweck: {a.Zweck}");
            return string.Join("\n", zeilen);
        }
    }
}
