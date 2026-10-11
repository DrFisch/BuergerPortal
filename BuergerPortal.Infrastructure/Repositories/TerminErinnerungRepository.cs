using BuergerPortal.Application.Appointments.Erinnerung;
using BuergerPortal.Domain.Appointments.Entity;
using BuergerPortal.Domain.Appointments.Enums;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuergerPortal.Infrastructure.Repositories
{
    public sealed class TerminErinnerungRepository(PortalDbContext db) : ITerminErinnerungRepository
    {
        public async Task<IReadOnlyList<Appointment>> VermerkeFaelligeAsync(DateTime jetztUtc, TimeSpan vorher, CancellationToken ct)
        {
            var bis = jetztUtc + vorher;
            var minuten = vorher.TotalMinutes;
            // Kurzfristig gebuchte Termine (erst im Erinnerungsfenster) bekommen keine Erinnerung – die Bestätigung genügt.
            // Termine von vor dieser Funktion (ohne Buchungszeitpunkt) ebenfalls nicht.
            var faellig = await db.Appointments
                .Where(a => a.Status == AppointmentStatus.Booked
                            && a.ErinnerungGesendetUtc == null
                            && a.GebuchtUtc != null
                            && a.StartUtc > jetztUtc && a.StartUtc <= bis
                            && a.GebuchtUtc <= a.StartUtc.AddMinutes(-minuten))
                .ToListAsync(ct);
            foreach (var termin in faellig)
            {
                termin.ErinnerungGesendetUtc = jetztUtc;
            }
            await db.SaveChangesAsync(ct);
            return faellig;
        }
    }
}
