using BuergerPortal.Application.Antraege.AntragSperrmuell.DTOs;
using BuergerPortal.Application.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Interfaces.BusinessServices
{
    public interface ISperrmuellAntragBusinessService
    {
        Task<Result<Guid>> CreateStep1Async(SperrmuellStep1Dto dto, Guid currentUserId, CancellationToken ct);
        Task<Result<bool>> UpdateStep2Async(Guid antragId, SperrmuellStep2Dto dto, Guid currentUserId, CancellationToken ct);
        Task<Result<bool>> SubmitAsync(Guid antragId, Guid currentUserId, CancellationToken ct);

        Task<Result<SperrmuellDetailDto>> GetAsync(Guid antragId, Guid currentUserId, CancellationToken ct);
        Task<IReadOnlyList<SperrmuellSummaryDto>> GetAllForUserAsync(Guid currentUserId, CancellationToken ct);
    }
}
