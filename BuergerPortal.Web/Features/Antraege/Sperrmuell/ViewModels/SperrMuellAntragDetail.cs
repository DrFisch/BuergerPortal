namespace BuergerPortal.Web.Features.Antraege.Sperrmuell.ViewModels
{
    public sealed class SperrmuellAntragDetailVm
    {
        public Guid Id { get; set; }

        // Antragsteller
        public string Antragsteller { get; set; } = string.Empty;
        public DateTime? Geburtsdatum { get; set; }

        // Kontakt
        public string? Email { get; set; }
        public string? Telefon { get; set; }

        // Statusanzeige (UI-mapped, wie bei Reisepass)
        public string StatusText { get; set; } = string.Empty;
        public string BadgeClass { get; set; } = "text-bg-secondary";
        public int ProgressPercent { get; set; }

        // Zeitpunkte
        public DateTime CreatedUtc { get; set; }
        public DateTime? SubmittedUtc { get; set; }

        // Adresse
        public string Strasse { get; set; } = string.Empty;
        public string PLZ { get; set; } = string.Empty;
        public string Ort { get; set; } = string.Empty;

        // Müllmengen
        public int? HolzKubikmeter { get; set; }
        public int? SonstigesKubikmeter { get; set; }
        public int? Matratzen { get; set; }

        // Abholzeit / Wunschzeit
        public DateTime Wunschzeit { get; set; }

        // Hinweis
        public string? Hinweis { get; set; }
    }
}
