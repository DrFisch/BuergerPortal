using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.Web.Features.Antraege.Meldebescheinigung.ViewModels
{
    /// <summary>
    /// Personendaten der Bescheinigung, wie sie die BundID-Anmeldung liefert (nicht im Formular änderbar – ein echtes
    /// Fachverfahren läse sie aus dem Melderegister).
    /// </summary>
    public sealed class MeldePerson
    {
        public string Vorname { get; init; } = string.Empty;
        public string Nachname { get; init; } = string.Empty;
        public DateOnly? Geburtsdatum { get; init; }
        public string? Geburtsort { get; init; }
        public string? Geburtsname { get; init; }
        public string? Strasse { get; init; }
        public string? PLZ { get; init; }
        public string? Ort { get; init; }

        public bool Vollstaendig => !string.IsNullOrWhiteSpace(Vorname) && !string.IsNullOrWhiteSpace(Nachname)
            && Geburtsdatum is not null && !string.IsNullOrWhiteSpace(Strasse) && !string.IsNullOrWhiteSpace(PLZ)
            && !string.IsNullOrWhiteSpace(Ort);

        public string Name => $"{Vorname} {Nachname}".Trim();
        public string Anschrift => $"{Strasse}, {PLZ} {Ort}";
    }

    /// <summary>Meldebescheinigung, Schritt 1 „Angaben“: Art, Zweck und E-Mail; Person aus der BundID.</summary>
    public sealed class MeldebescheinigungAngabenVm
    {
        public Guid? Id { get; set; }

        [Range(1, 2)]
        public int Art { get; set; } = 1;

        [StringLength(200)]
        public string? Zweck { get; set; }

        [EmailAddress]
        [StringLength(200)]
        public string? Email { get; set; }

        public MeldePerson Person { get; set; } = new();
    }

    /// <summary>Schritt 2 „Prüfen und absenden“ mit dem nötigen Niveau „hoch“ (Step-up, wenn die Anmeldung nicht reicht).</summary>
    public sealed class MeldebescheinigungPruefenVm
    {
        public Guid Id { get; init; }
        public MeldePerson Person { get; init; } = new();
        public int Art { get; init; }
        public string? Zweck { get; init; }
        public string? Email { get; init; }
        public int Niveau { get; init; }
        public int NiveauNoetig { get; init; }
        public bool NiveauReicht => Leistungen.NiveauReicht(Niveau, NiveauNoetig);
        public string Vorgangsnummer => Id.ToString("N")[..8].ToUpperInvariant();
    }

    /// <summary>Detailseite und Bescheinigung (Druckansicht).</summary>
    public sealed class MeldebescheinigungDetailVm
    {
        public Guid Id { get; init; }
        public int Status { get; init; }
        public MeldePerson Person { get; init; } = new();
        public int Art { get; init; }
        public string? Zweck { get; init; }
        public string? Email { get; init; }
        public DateTime CreatedUtc { get; init; }
        public DateTime? SubmittedUtc { get; init; }
        public DateTime? InBearbeitungSeit { get; init; }
        public DateTime? AbgeschlossenAm { get; init; }
        public bool Erweitert => Art == 2;
        public string Vorgangsnummer => Id.ToString("N")[..8].ToUpperInvariant();
    }
}
