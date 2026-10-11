using BuergerPortal.Domain.Appointments.Entity;
using BuergerPortal.Domain.Appointments.Enums;

namespace BuergerPortal.Application.Appointments.Erinnerung
{
    /// <summary>Alles für eine Erinnerung vor dem Termin (Postfach-Nachricht und Benachrichtigung).</summary>
    public sealed record TerminErinnerung(Guid TerminId, Guid UserId, string? PostkorbHandle, ServiceType Service,
        LocationType Location, DateTime StartUtc);

    public interface ITerminErinnerungRepository
    {
        /// <summary>
        /// Gebuchte, noch nicht erinnerte Termine, die zwischen <paramref name="jetztUtc"/> und
        /// <paramref name="jetztUtc"/> + <paramref name="vorher"/> beginnen und früh genug gebucht wurden (vor Beginn des
        /// Erinnerungsfensters) – als erinnert vermerkt und zurückgegeben (jede Erinnerung höchstens einmal).
        /// </summary>
        Task<IReadOnlyList<Appointment>> VermerkeFaelligeAsync(DateTime jetztUtc, TimeSpan vorher, CancellationToken ct);
    }

    public interface ITerminErinnerungService
    {
        Task<IReadOnlyList<TerminErinnerung>> FaelligeAsync(TimeSpan vorher, CancellationToken ct);
    }

    public sealed class TerminErinnerungService(ITerminErinnerungRepository repo, TimeProvider time) : ITerminErinnerungService
    {
        public async Task<IReadOnlyList<TerminErinnerung>> FaelligeAsync(TimeSpan vorher, CancellationToken ct)
        {
            var termine = await repo.VermerkeFaelligeAsync(time.GetUtcNow().UtcDateTime, vorher, ct);
            return termine.Select(t => new TerminErinnerung(t.Id, t.UserId, t.PostkorbHandle, t.Service, t.Location,
                DateTime.SpecifyKind(t.StartUtc, DateTimeKind.Utc))).ToList();
        }
    }
}
