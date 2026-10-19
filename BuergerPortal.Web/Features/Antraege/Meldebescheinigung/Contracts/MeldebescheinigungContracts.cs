namespace BuergerPortal.Web.Features.Antraege.Meldebescheinigung.Contracts
{
    /// <summary>Anlegen/Ändern eines Entwurfs (api/antraege/meldebescheinigung), Art: 1 einfach, 2 erweitert.</summary>
    public sealed class MeldebescheinigungRequest
    {
        public string Vorname { get; set; } = string.Empty;
        public string Nachname { get; set; } = string.Empty;
        public DateOnly Geburtsdatum { get; set; }
        public string? Geburtsort { get; set; }
        public string? Geburtsname { get; set; }
        public string Strasse { get; set; } = string.Empty;
        public string PLZ { get; set; } = string.Empty;
        public string Ort { get; set; } = string.Empty;
        public string? Email { get; set; }
        public int Art { get; set; } = 1;
        public string? Zweck { get; set; }
    }

    public sealed class MeldebescheinigungDetailResponse
    {
        public Guid Id { get; set; }
        public int Typ { get; set; }
        public int Status { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? SubmittedUtc { get; set; }
        public string Vorname { get; set; } = string.Empty;
        public string Nachname { get; set; } = string.Empty;
        public DateOnly Geburtsdatum { get; set; }
        public string? Geburtsort { get; set; }
        public string? Geburtsname { get; set; }
        public string Strasse { get; set; } = string.Empty;
        public string PLZ { get; set; } = string.Empty;
        public string Ort { get; set; } = string.Empty;
        public string? Email { get; set; }
        public int Art { get; set; }
        public string? Zweck { get; set; }
    }

    public sealed class MeldebescheinigungSummaryResponse
    {
        public Guid Id { get; set; }
        public int Typ { get; set; }
        public int Status { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? SubmittedUtc { get; set; }
        public string Vorname { get; set; } = string.Empty;
        public string Nachname { get; set; } = string.Empty;
    }
}
