using BuergerPortal.Domain.Appointments.Enums;

namespace BuergerPortal.Application.Interfaces.Repositories
{
    /// <summary>
    /// Sperre für „prüfen, ob frei – dann speichern“ beim Buchen und Umbuchen. Ohne Sperre könnten zwei gleichzeitige
    /// Anfragen beide „frei“ sehen und denselben Zeitraum am selben Bürgeramt buchen. Ein eindeutiger Index reicht nicht,
    /// weil sich Termine mit 15/30/45 Minuten überlappen, ohne gleich zu beginnen.
    /// Gesperrt wird je Person und je Standort, immer in dieser Reihenfolge (keine gegenseitige Blockade).
    /// </summary>
    public interface IAppointmentBookingLock
    {
        /// <summary>
        /// Wartet, bis Person und Standort frei sind. Die Sperre gilt bis <see cref="IAppointmentBookingLease.CommitAsync"/>
        /// oder bis zum Freigeben ohne Bestätigung (dann wird nichts gespeichert).
        /// </summary>
        /// <exception cref="TimeoutException">Sperre nicht innerhalb der Wartezeit erhalten.</exception>
        Task<IAppointmentBookingLease> AcquireAsync(Guid userId, LocationType location, CancellationToken ct);
    }

    /// <summary>Gehaltene Sperre; Freigeben ohne <see cref="CommitAsync"/> verwirft die Änderungen.</summary>
    public interface IAppointmentBookingLease : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken ct);
    }
}
