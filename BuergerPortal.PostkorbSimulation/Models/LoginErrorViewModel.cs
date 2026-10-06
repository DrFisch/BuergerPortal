namespace BuergerPortal.PostkorbSimulation.Models;

/// <summary>Fehlerseite der BundID-Anmeldung am Postfach.</summary>
public sealed class LoginErrorViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string RetryUrl { get; init; } = "/bundid/login";
}
