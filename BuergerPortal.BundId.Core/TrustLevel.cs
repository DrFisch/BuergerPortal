namespace BuergerPortal.BundId
{
    /// <summary>
    /// Vertrauensniveaus der BundID (STORK-QAA-Level): 1 = normal (Benutzername/Passwort),
    /// 3 = substanziell (z. B. ELSTER-Zertifikat), 4 = hoch (Online-Ausweis/eID).
    /// </summary>
    public static class TrustLevel
    {
        public const int Normal = 1;
        public const int Substantial = 3;
        public const int High = 4;

        private const string StorkPrefix = "STORK-QAA-Level-";

        public static string ToStork(int level) => StorkPrefix + level;

        public static string Describe(int level) => level switch
        {
            >= High => "hoch",
            >= Substantial => "substanziell",
            _ => "normal",
        };

        /// <summary>
        /// Liest ein Niveau aus einem AuthnContextClassRef bzw. dem Attribut EID-CITIZEN-QAA-LEVEL:
        /// STORK-Bezeichner (so fordert die BundID das Niveau an) oder eIDAS-LoA-URI (so liefert es der
        /// Simulator-Fork, weil SAML absolute URIs verlangt). Unbekannte Werte ergeben null.
        /// </summary>
        public static int? Parse(string? value)
        {
            var text = value?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            if (text.StartsWith(StorkPrefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(text[StorkPrefix.Length..], out var stork) && stork is >= 1 and <= 4)
            {
                return stork;
            }
            return text switch
            {
                "http://eidas.europa.eu/LoA/high" => High,
                "http://eidas.europa.eu/LoA/substantial" => Substantial,
                "http://eidas.europa.eu/LoA/low" => Normal,
                _ => null,
            };
        }
    }
}
