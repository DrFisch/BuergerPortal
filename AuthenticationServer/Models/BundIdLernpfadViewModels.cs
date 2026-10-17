using BuergerPortal.BundId;

namespace AuthenticationServer.Models
{
    /// <summary>Lernpfad Schritt 2: der AuthnRequest an die BundID, bevor er abgeschickt wird.</summary>
    public sealed class LernpfadAuthnRequestViewModel
    {
        public required string Destination { get; init; }
        public required string SamlRequest { get; init; }
        public required string Xml { get; init; }
        public required string RequestId { get; init; }
        public required string Issuer { get; init; }
        public required string AssertionConsumerServiceUrl { get; init; }
        public required string RequestedLevel { get; init; }
        public required bool ForceAuthn { get; init; }
        public required string CancelUrl { get; init; }
    }

    /// <summary>Lernpfad Schritt 4: die geprüfte Response und wohin es weitergeht.</summary>
    public sealed class LernpfadResponseViewModel
    {
        public BundIdInspection? Inspection { get; init; }
        public required string ContinueUrl { get; init; }
        public required bool AccountCreated { get; init; }
    }
}
