namespace BuergerPortal.Application.Interfaces.Postkorb
{
    /// <summary>
    /// Nachricht an das BundID-Postfach einer Person – vereinfachtes Abbild von "CreateMessage" des
    /// Zentralen Bürgerpostfachs (ZBP). Absender und Antwortadresse setzt die Implementierung aus der Konfiguration.
    /// </summary>
    /// <param name="PostkorbHandle">Postkorb-Handle aus der BundID-Anmeldung (Claim "postkorb_handle").</param>
    /// <param name="Title">Betreff.</param>
    /// <param name="Content">Nachrichtentext (Klartext).</param>
    /// <param name="Service">Verwaltungsleistung, z. B. "Terminvereinbarung".</param>
    /// <param name="StorkQaaLevel">Vertrauensniveau, ab dem die Nachricht lesbar ist (1, 3 oder 4).</param>
    public sealed record PostkorbMessage(string? PostkorbHandle, string Title, string Content, string Service,
        int StorkQaaLevel = 1);

    public enum PostkorbDeliveryStatus
    {
        Delivered,
        // Kein Postkorb-Handle (z. B. altes Konto ohne BundID-Anmeldung) – nichts zuzustellen.
        NoMailbox,
        // Postkorb nicht erreichbar oder Nachricht abgelehnt.
        Failed,
    }

    /// <summary>Zustellung in das BundID-Postfach (hier: die Postkorb-Simulation).</summary>
    public interface IPostkorbService
    {
        /// <summary>
        /// Stellt die Nachricht zu. Wirft keine Ausnahme: Ein nicht erreichbares Postfach darf einen
        /// Fachvorgang (Buchung, Antrag) nicht scheitern lassen; der Aufrufer kann auf das Ergebnis hinweisen.
        /// </summary>
        Task<PostkorbDeliveryStatus> SendAsync(PostkorbMessage message, CancellationToken ct = default);
    }
}
