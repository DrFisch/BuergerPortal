using BuergerPortal.Application.Antraege.Status;
using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Domain.Antrag.Enums;

namespace BuergerPortal.Application.Interfaces.Repositories
{
    /// <summary>Statusverlauf der Anträge (Zeitleiste, Statusmeldungen ins BundID-Postfach).</summary>
    public interface IAntragStatusRepository
    {
        /// <summary>
        /// Status am Antrag setzen und den Wechsel festhalten – in einem Speichervorgang. Ein übergebenes Postkorb-Handle
        /// wird am Antrag gespeichert. false, wenn es den Antrag nicht gibt.
        /// </summary>
        Task<bool> SetStatusAsync(Guid antragId, AntragStatus status, DateTime zeitpunktUtc, string? postkorbHandle,
            CancellationToken ct);

        /// <summary>Statuswechsel eines Antrags, älteste zuerst.</summary>
        Task<IReadOnlyList<AntragStatusEreignis>> GetVerlaufAsync(Guid antragId, CancellationToken ct);

        Task<bool> GehoertPersonAsync(Guid antragId, Guid userId, CancellationToken ct);

        /// <summary>
        /// Anträge, die seit mindestens bis <paramref name="vorUtc"/> im Status <paramref name="status"/> sind (laut
        /// Statusverlauf) – für die simulierte Bearbeitung. Anträge ohne Verlauf (vor dieser Funktion eingereicht) nie.
        /// </summary>
        Task<IReadOnlyList<Antrag>> GetFaelligeAsync(AntragStatus status, DateTime vorUtc, CancellationToken ct);

        /// <summary>
        /// Eigenen Entwurf löschen (samt Verlauf). Eingereichte Anträge bleiben – sie gehören zum Vorgang der Behörde.
        /// </summary>
        Task<EntwurfVerwerfenErgebnis> EntwurfLoeschenAsync(Guid antragId, Guid userId, CancellationToken ct);
    }
}
