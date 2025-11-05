using BuergerPortal.Application.Antraege.AntragReisepass.DTOs;
using BuergerPortal.Application.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Interfaces.BusinessServices
{
    public interface IReisepassAntragBusinessService
    {
        Task<Result<Guid>> CreateStep1Async(ReisepassStep1Dto dto, Guid currentUserId, CancellationToken ct);
        Task<Result<bool>> UpdateStep2Async(Guid antragId, ReisepassStep2Dto dto, Guid currentUserId, CancellationToken ct);
        Task<Result<bool>> SubmitAsync(Guid antragId, Guid currentUserId, CancellationToken ct);

        Task<Result<ReisepassDetailDto>> GetAsync(Guid antragId, Guid currentUserId, CancellationToken ct);
        Task<IReadOnlyList<ReisepassSummaryDto>> GetAllForUserAsync(Guid currentUserId, CancellationToken ct);
    }
}
