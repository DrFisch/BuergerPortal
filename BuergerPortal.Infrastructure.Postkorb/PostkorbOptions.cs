using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.Infrastructure.Postkorb
{
    /// <summary>Anbindung an das BundID-Postfach (Abschnitt "Postkorb").</summary>
    public sealed class PostkorbOptions
    {
        public const string SectionName = "Postkorb";

        // Adresse der Postkorb-Simulation, im Container-Netz z. B. http://postkorb:8080/.
        [Required, Url]
        public string BaseUrl { get; set; } = string.Empty;

        // Gemeinsamer Schlüssel mit der Postkorb-Simulation. Im Betrieb nur per Umgebungsvariable Postkorb__ApiKey.
        [Required, MinLength(32)]
        public string ApiKey { get; set; } = string.Empty;

        // Absendende Stelle, wie sie im Postfach erscheint.
        [Required]
        public string Sender { get; set; } = "BürgerPortal (Studienprojekt)";

        public string? ReplyAddress { get; set; }

        // Kurz halten: Die Buchung wartet auf die Zustellung.
        [Range(1, 30)]
        public int TimeoutSeconds { get; set; } = 5;
    }
}
