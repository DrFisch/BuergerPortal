namespace BuergerPortal.Web.Features.Antraege.Reisepass.ViewModels
{
    /// <summary>
    /// Reisepass, Schritt 3 „Prüfen und absenden“ (2026-10-18): alle Angaben, die Gebühr und ob das Niveau der Anmeldung
    /// reicht – sonst erklärt die Seite den Step-up, bevor er passiert.
    /// </summary>
    public sealed class ReisepassPruefenVm
    {
        public Guid Id { get; init; }
        public string Antragsteller { get; init; } = string.Empty;
        public DateTime Geburtsdatum { get; init; }
        public string? Email { get; init; }
        public string? Telefon { get; init; }
        public bool Express { get; init; }
        public bool AltpassVorhanden { get; init; }
        public string? Hinweis { get; init; }
        public decimal Gebuehr { get; init; }

        /// <summary>Erreichtes Niveau der Anmeldung und das, was der Reisepass verlangt.</summary>
        public int Niveau { get; init; }
        public int NiveauNoetig { get; init; }
        public bool NiveauReicht => Leistungen.NiveauReicht(Niveau, NiveauNoetig);

        public string Vorgangsnummer => Id.ToString("N")[..8].ToUpperInvariant();
    }
}
