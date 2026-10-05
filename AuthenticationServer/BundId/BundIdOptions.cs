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

        // Attribute, die im AuthnRequest angefordert werden (AKDB-Extension). Nur was das Portal braucht.
        public List<BundIdRequestedAttribute> RequestedAttributes { get; set; } = [];

        // SAML-Attribut mit dem Postkorb-Handle. Bei der echten BundID nicht öffentlich dokumentiert;
        // urn:oid:2.5.4.18 (postOfficeBox) ist eine Annahme und kommt so vom Simulator-Fork.
        public string PostkorbHandleAttribute { get; set; } = "urn:oid:2.5.4.18";

        // Anzeige auf der BundID-Seite: Name der Organisation und Kennung des Online-Dienstes.
        public string OrganizationDisplayName { get; set; } = string.Empty;
        public string OnlineServiceId { get; set; } = string.Empty;
    }

    /// <summary>Ein angefordertes BundID-Attribut: Name = OID (urn:oid:...), Required = Pflichtattribut.</summary>
    public sealed class BundIdRequestedAttribute
    {
        public string Name { get; set; } = string.Empty;
        public bool Required { get; set; }
    }
}
