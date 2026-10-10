using BuergerPortal.Domain.Push.Entity;

namespace BuergerPortal.Application.Interfaces.Repositories
{
    /// <summary>Gespeicherte Push-Abos (Web Push) je Person.</summary>
    public interface IPushSubscriptionRepository
    {
        /// <summary>
        /// Legt das Abo an oder aktualisiert es (gleicher Endpoint). Meldet sich auf demselben Gerät eine andere
        /// Person an, gehört das Abo danach ihr – Nachrichten der vorherigen Person landen dort nicht mehr.
        /// </summary>
        Task SaveAsync(PushSubscription subscription, CancellationToken ct);

        Task<IReadOnlyList<PushSubscription>> GetByUserIdAsync(Guid userId, CancellationToken ct);

        /// <summary>Entfernt ein eigenes Abo; false, wenn es der Person nicht gehört oder nicht existiert.</summary>
        Task<bool> DeleteAsync(Guid userId, string endpoint, CancellationToken ct);

        /// <summary>Entfernt ein Abo, das der Push-Dienst nicht mehr kennt (Antwort 404/410).</summary>
        Task DeleteByEndpointAsync(string endpoint, CancellationToken ct);
    }
}
