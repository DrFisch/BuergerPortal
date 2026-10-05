namespace AuthenticationServer.Models
{
    /// <summary>Vorläufige Ergebnisseite der BundID-Anmeldung (bis die Benutzeranmeldung angebunden ist).</summary>
    public sealed class BundIdResultViewModel
    {
        public bool Success { get; init; }
        public string Status { get; init; } = string.Empty;
        public string? Message { get; init; }
        public IReadOnlyList<KeyValuePair<string, string>> Claims { get; init; } = [];
    }
}
