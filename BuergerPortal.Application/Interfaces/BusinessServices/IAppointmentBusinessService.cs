using BuergerPortal.Application.Appointments.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Interfaces.BusinessServices
{
    public interface IAppointmentBusinessService
    {
        Task<Guid> BookAsync(AppointmentCreateDto dto, string currentUserId, CancellationToken ct);
    }
}
