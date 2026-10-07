using BuergerPortal.Domain.Appointments.Entity;
using BuergerPortal.Domain.Appointments.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Interfaces.Repositories
{
    public interface IAppointmentRepository
    {
        /// <summary>Hat die Person im Zeitraum schon einen gebuchten Termin (egal an welchem Standort)?</summary>
        Task<bool> ExistsOverlapAsync(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken ct);

        /// <summary>Ist der Zeitraum am Standort schon vergeben (ein Schalter je Bürgeramt)? excludeId: eigener Termin.</summary>
        Task<bool> ExistsLocationOverlapAsync(LocationType location, DateTime startUtc, DateTime endUtc, Guid? excludeId,
            CancellationToken ct);

        Task CreateAsync(Appointment entity, CancellationToken ct);

        Task<List<Appointment>> GetAllForUserAsync(Guid userId, CancellationToken ct);
        // Optional für später:
        // Task<Appointment?> GetAsync(Guid id, CancellationToken ct);
        // Task DeleteAsync(Guid id, CancellationToken ct);
        /// <summary>Gebuchte Termine im Zeitraum, optional nur an einem Standort.</summary>
        Task<List<Appointment>> GetOverlappingAsync(DateTime fromUtc, DateTime toUtc, LocationType? location,
            CancellationToken ct);
        Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct);
        Task UpdateAsync(Appointment entity, CancellationToken ct);
        Task DeleteAsync(Appointment entity, CancellationToken ct);
    }
}
