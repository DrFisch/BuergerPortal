namespace AuthenticationServer.Models
{
    /// <summary>Vorläufige Ergebnisseite der BundID-Anmeldung (bis die Benutzeranmeldung angebunden ist).</summary>
    public sealed class BundIdResultViewModel
    {
        public IReadOnlyList<KeyValuePair<string, string>> Claims { get; init; } = [];
    }

    /// <summary>Fehlerseite der BundID-Anmeldung mit verständlichem Text und Link für einen neuen Versuch.</summary>
    public sealed class BundIdErrorViewModel
    {
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string RetryUrl { get; init; } = "/bundid/login";
    }
}
