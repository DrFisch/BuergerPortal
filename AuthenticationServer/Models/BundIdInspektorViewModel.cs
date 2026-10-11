using AuthenticationServer.BundId;

namespace AuthenticationServer.Models
{
    /// <summary>Seite des SAML-Inspektors: Protokoll der eigenen Anmeldung (oder keins) und die Links zum Portal.</summary>
    public sealed class BundIdInspektorViewModel
    {
        public BundIdInspectionStore.Entry? Entry { get; init; }
        public int MinutesVisible { get; init; }
        public string? PortalUrl { get; init; }

        public string? BackUrl => PortalUrl == null ? null : PortalUrl + "/MeineDaten";

        // Neue Anmeldung über das Portal (OIDC → SAML), danach zurück in den Inspektor.
        public string? NewLoginUrl => PortalUrl == null ? null : PortalUrl + "/MeineDaten/SamlInspektorNeu";
    }
}
