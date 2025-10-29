using BuergerPortal.Application.Appointments.DTOs;
using BuergerPortal.Application.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Interfaces.BusinessServices
{
    public interface IAppointmentBusinessService
    {
        Task<Result<Guid>> BookAsync(AppointmentCreateDto dto, string currentUserId, CancellationToken ct);

        Task<List<AppointmentListItemDto>> GetAllForUserAsync(string userId, CancellationToken ct);
    }

}

