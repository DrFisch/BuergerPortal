using BuergerPortal.Domain.Antrag.Enums;
using BuergerPortal.Domain.Antrag.ValueObjects;

namespace BuergerPortal.Domain.Antrag.Entity
{
    /// <summary>
    /// Antrag auf eine Meldebescheinigung (2026-10-18). Vorbild: Online-Dienst des Landes Berlin (Online-Ausweis
    /// nötig, online gebührenfrei, Bescheinigung elektronisch). Die Personendaten stammen aus der BundID-Anmeldung
    /// (Stand beim Anlegen bzw. letzten Ändern) – ein echtes Fachverfahren läse sie aus dem Melderegister.
    /// </summary>
    public sealed class MeldebescheinigungAntrag : Antrag
    {
        public MeldebescheinigungAntrag()
        {
            Typ = AntragTyp.Meldebescheinigung;
        }

        public PersonName Name { get; set; } = default!;
        public PersonBirth Birth { get; set; } = default!;
        public string? Geburtsort { get; set; }
        public string? Geburtsname { get; set; }
        public Addresse Addresse { get; set; } = default!;
        public Kontakt Kontakt { get; set; } = new(null, null);

        public MeldebescheinigungArt Art { get; set; } = MeldebescheinigungArt.Einfach;

        /// <summary>Wofür die Bescheinigung gebraucht wird (freiwillig, z. B. „für den Arbeitgeber“).</summary>
        public string? Zweck { get; set; }
    }
}
