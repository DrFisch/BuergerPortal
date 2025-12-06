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
        Task<Result<Guid>> BookAsync(AppointmentCreateDto dto, Guid currentUserId, CancellationToken ct);

        Task<List<AppointmentListItemDto>> GetAllForUserAsync(Guid userId, CancellationToken ct);
        Task<List<BusySlotDto>> GetBusyAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);
        Task<Result<Guid>> CancelAsync(Guid id, Guid currentUserId, CancellationToken ct);
        Task<Result<Guid>> DeleteAsync(Guid id, Guid currentUserId, CancellationToken ct); // optional (Hard-Delete)
    }
}



