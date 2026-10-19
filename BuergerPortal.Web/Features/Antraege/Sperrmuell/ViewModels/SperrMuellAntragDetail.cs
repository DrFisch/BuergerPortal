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
        // Status wie die API (1 Entwurf … 5 abgelehnt) – Zeitleiste und „Nächste Schritte“ (2026-10-18)
        public int Status { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string BadgeClass { get; set; } = "text-bg-secondary";
        public int ProgressPercent { get; set; }

        // Zeitpunkte
        public DateTime CreatedUtc { get; set; }
        public DateTime? SubmittedUtc { get; set; }
        // Zeitleiste: Zeitpunkte aus dem Statusverlauf der API (deutsche Zeit); bei älteren Anträgen ohne Verlauf leer
        public DateTime? InBearbeitungSeit { get; set; }
        public DateTime? AbgeschlossenAm { get; set; }

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
