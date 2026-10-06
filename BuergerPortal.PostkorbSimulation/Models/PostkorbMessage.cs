namespace BuergerPortal.PostkorbSimulation.Models;

/// <summary>
/// Nachricht im simulierten BundID-Postfach. Die Felder folgen "CreateMessage" des Zentralen Bürgerpostfachs (ZBP),
/// soweit öffentlich dokumentiert (FIT-Connect, https://docs.fitko.de/fit-connect/docs/zbp/).
/// Nicht nachgebaut: Anhänge, Abrufbestätigung an retrievalConfirmationAddress, BPKI-Signatur (sha512sum).
/// </summary>
public class PostkorbMessage
{
    // Belegt ist nur "content max. 1 MB"; die übrigen Längen sind eigene, großzügige Grenzen der Simulation.
    public const int MaxContentLength = 1024 * 1024;
    public const int MaxTitleLength = 255;
    public const int MaxNameLength = 255;
    public const int MaxAddressLength = 320;

    public Guid Id { get; set; }

    /// <summary>Postkorb-Handle der Person (ZBP: mailboxUuid). Die BundID liefert es beim Login als Attribut.</summary>
    public Guid MailboxUuid { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Nachrichtentext (Klartext).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Absendende Stelle, z. B. "Bürgerbüro Musterstadt".</summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>Verwaltungsleistung, zu der die Nachricht gehört, z. B. "Terminvereinbarung".</summary>
    public string Service { get; set; } = string.Empty;

    /// <summary>Optionale Antwortadresse der absendenden Stelle (E-Mail).</summary>
    public string? ReplyAddress { get; set; }

    /// <summary>Vertrauensniveau der Nachricht (1, 3 oder 4; ZBP: stork_qaa_level).</summary>
    public int StorkQaaLevel { get; set; } = 1;

    public DateTime CreatedUtc { get; set; }

    /// <summary>Zeitpunkt des ersten Öffnens im Postfach; null = ungelesen.</summary>
    public DateTime? ReadUtc { get; set; }
}
