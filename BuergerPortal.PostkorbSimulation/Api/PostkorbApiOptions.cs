using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.PostkorbSimulation.Api;

/// <summary>Einstellungen der REST-Schnittstelle (Abschnitt "PostkorbApi").</summary>
public sealed class PostkorbApiOptions
{
    public const string SectionName = "PostkorbApi";

    /// <summary>
    /// Gemeinsamer Schlüssel der absendenden Dienste. Im Betrieb nur per Umgebungsvariable PostkorbApi__ApiKey
    /// setzen, nie in eine Datei im Repository.
    /// </summary>
    [Required, MinLength(32)]
    public string ApiKey { get; set; } = string.Empty;
}
