namespace AuthenticationServer.Models
{
    /// <summary>Fehlerseite der BundID-Anmeldung mit verständlichem Text und Link für einen neuen Versuch.</summary>
    public sealed class BundIdErrorViewModel
    {
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string RetryUrl { get; init; } = "/bundid/login";
    }
}
