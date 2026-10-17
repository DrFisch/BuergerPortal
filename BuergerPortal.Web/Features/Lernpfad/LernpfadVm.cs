using BuergerPortal.Web.Services;

namespace BuergerPortal.Web.Features.Lernpfad
{
    /// <summary>Ein Parameter bzw. Claim mit (ggf. gekürztem) Wert; die Erklärung kommt aus den Ressourcen der Seite.</summary>
    public sealed record LernpfadParameter(string Name, string Wert);

    /// <summary>Schritt 1: die OIDC-Anfrage, wie sie gleich zum Anmeldedienst geht.</summary>
    public sealed class LernpfadAnfrageVm
    {
        public required string Endpunkt { get; init; }
        public required string WeiterUrl { get; init; }
        public required IReadOnlyList<LernpfadParameter> Parameter { get; init; }
    }

    /// <summary>Schritt 5: was beim Code-Tausch ankam und welche Angaben das Portal jetzt hat.</summary>
    public sealed class LernpfadAngekommenVm
    {
        public LernpfadTokenInfo? Info { get; init; }
        public required string Ziel { get; init; }
        public required IReadOnlyList<LernpfadParameter> Claims { get; init; }
    }
}
