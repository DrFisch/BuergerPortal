using BuergerPortal.Application.Antraege.Status;

namespace BuergerPortal.Application.Interfaces.BusinessServices
{
    public interface IAntragStatusService
    {
        /// <summary>Einreichen festhalten (Zeitleiste) und das Postkorb-Handle für spätere Statusmeldungen merken.</summary>
        Task EingereichtAsync(Guid antragId, string? postkorbHandle, CancellationToken ct);

        /// <summary>Zeitleiste des eigenen Antrags; null, wenn der Antrag nicht der Person gehört oder fehlt.</summary>
        Task<IReadOnlyList<AntragStatusEreignisDto>?> GetVerlaufAsync(Guid antragId, Guid userId, CancellationToken ct);

        /// <summary>
        /// Simulierte Bearbeitung (es gibt keine Behördensicht): eingereicht → in Bearbeitung nach
        /// <paramref name="bisInBearbeitung"/>, in Bearbeitung → genehmigt nach <paramref name="bisAbschluss"/>.
        /// Liefert die vorgenommenen Wechsel für Statusmeldungen und Benachrichtigungen.
        /// </summary>
        Task<IReadOnlyList<AntragStatuswechsel>> BearbeitungSimulierenAsync(TimeSpan bisInBearbeitung, TimeSpan bisAbschluss,
            CancellationToken ct);
    }
}
