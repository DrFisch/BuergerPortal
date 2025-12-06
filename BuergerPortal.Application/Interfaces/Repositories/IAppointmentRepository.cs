using BuergerPortal.Domain.Appointments.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Interfaces.Repositories
{
    public interface IAppointmentRepository
    {
        Task<bool> ExistsOverlapAsync(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken ct);

        Task CreateAsync(Appointment entity, CancellationToken ct);

        Task<List<Appointment>> GetAllForUserAsync(Guid userId, CancellationToken ct);
        // Optional für später:
        // Task<Appointment?> GetAsync(Guid id, CancellationToken ct);
        // Task DeleteAsync(Guid id, CancellationToken ct);
        Task<List<Appointment>> GetOverlappingAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);
        Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct);
        Task UpdateAsync(Appointment entity, CancellationToken ct);
        Task DeleteAsync(Appointment entity, CancellationToken ct);
    }
}
