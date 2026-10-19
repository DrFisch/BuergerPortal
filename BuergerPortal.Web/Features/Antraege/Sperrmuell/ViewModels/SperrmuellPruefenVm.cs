namespace BuergerPortal.Web.Features.Antraege.Sperrmuell.ViewModels
{
    /// <summary>Sperrmüll, Schritt 3 „Prüfen und absenden“ (2026-10-18): alle Angaben auf einen Blick.</summary>
    public sealed class SperrmuellPruefenVm
    {
        public Guid Id { get; init; }
        public string Antragsteller { get; init; } = string.Empty;
        public DateTime Geburtsdatum { get; init; }
        public string? Email { get; init; }
        public string? Telefon { get; init; }
        public string Strasse { get; init; } = string.Empty;
        public string PLZ { get; init; } = string.Empty;
        public string Ort { get; init; } = string.Empty;
        public int? HolzKubikmeter { get; init; }
        public int? SonstigesKubikmeter { get; init; }
        public int? Matratzen { get; init; }
        public DateTime Wunschzeit { get; init; }
        public string? Hinweis { get; init; }

        /// <summary>Holz und Sonstiges zusammen in Kubikmetern (Matratzen zählen als Stück).</summary>
        public int Kubikmeter => (HolzKubikmeter ?? 0) + (SonstigesKubikmeter ?? 0);

        public string Vorgangsnummer => Id.ToString("N")[..8].ToUpperInvariant();
    }
}
