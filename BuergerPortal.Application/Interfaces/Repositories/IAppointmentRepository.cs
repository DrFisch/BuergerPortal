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
        Task<bool> ExistsOverlapAsync(string userId, DateTime startUtc, DateTime endUtc, CancellationToken ct);

        Task CreateAsync(Appointment entity, CancellationToken ct);
        // Optional für später:
        // Task<Appointment?> GetAsync(Guid id, CancellationToken ct);
        // Task DeleteAsync(Guid id, CancellationToken ct);
    }
}
