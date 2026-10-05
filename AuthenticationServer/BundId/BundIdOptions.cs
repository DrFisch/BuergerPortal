namespace AuthenticationServer.BundId
{
    /// <summary>
    /// Einstellungen für die Anmeldung über die BundID (Abschnitt "BundId" in appsettings,
    /// per Umgebungsvariable überschreibbar, z. B. BundId__IdpMetadataUrl).
    /// </summary>
    public sealed class BundIdOptions
    {
        public const string SectionName = "BundId";

        // EntityID dieses Service Providers (des Auth-Servers) – unter diesem Namen kennt ihn die BundID.
        public string SpEntityId { get; set; } = string.Empty;

        // Assertion Consumer Service: an diese Adresse schickt die BundID die SAML-Response (HTTP-POST).
        public string AssertionConsumerServiceUrl { get; set; } = string.Empty;

        // Metadaten der BundID bzw. des Simulators: EntityID, SSO-Adresse und Signaturzertifikat des IdP.
        public string IdpMetadataUrl { get; set; } = string.Empty;

        // Vertrauensniveau für einen normalen Login (STORK-QAA-Level 1, 3 oder 4).
        public int DefaultTrustLevel { get; set; } = 1;
    }
}
