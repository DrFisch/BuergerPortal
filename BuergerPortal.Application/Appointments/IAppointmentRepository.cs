using BuergerPortal.Domain.Appointments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Appointments
{
    public interface IAppointmentRepository
    {
        Task CreateAsync(Appointment entity, CancellationToken ct);
        // (später) Task DeleteAsync(Guid id, CancellationToken ct);
    }
}
