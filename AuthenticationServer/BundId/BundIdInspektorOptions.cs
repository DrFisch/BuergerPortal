namespace AuthenticationServer.BundId
{
    /// <summary>
    /// Einstellungen des SAML-Inspektors (Abschnitt "BundId:Inspektor", z. B. BundId__Inspektor__Enabled; im Betrieb über
    /// BPSIM_INSPEKTOR in der .env). Der Inspektor zeigt nach der Anmeldung die gerade empfangene Assertion und jede Prüfung.
    /// </summary>
    public sealed class BundIdInspektorOptions
    {
        public const string SectionName = "BundId:Inspektor";

        // Standardmäßig an (Wunsch Lukas 2026-10-10). false: /bundid/inspektor antwortet 404, nichts wird aufbewahrt.
        public bool Enabled { get; set; } = true;

        // So lange nach der Anmeldung bleibt die Assertion sichtbar (nur im Arbeitsspeicher).
        public int MinutesVisible { get; set; } = 5;

        // Adresse des Portals für "Zurück zum BürgerPortal" und "Neu anmelden", z. B. https://bpsimulation.<domain>;
        // leer: keine Links.
        public string? PortalUrl { get; set; }
    }
}
