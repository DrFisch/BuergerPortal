using BuergerPortal.Application.Antraege.AntragMeldebescheinigung.DTOs;
using BuergerPortal.Application.Common;

namespace BuergerPortal.Application.Interfaces.BusinessServices
{
    public interface IMeldebescheinigungAntragBusinessService
    {
        Task<Result<Guid>> CreateAsync(MeldebescheinigungDto dto, Guid currentUserId, CancellationToken ct);
        Task<Result<bool>> UpdateAsync(Guid antragId, MeldebescheinigungDto dto, Guid currentUserId, CancellationToken ct);
        Task<Result<bool>> SubmitAsync(Guid antragId, Guid currentUserId, CancellationToken ct);

        Task<Result<MeldebescheinigungDetailDto>> GetAsync(Guid antragId, Guid currentUserId, CancellationToken ct);
        Task<IReadOnlyList<MeldebescheinigungSummaryDto>> GetAllForUserAsync(Guid currentUserId, CancellationToken ct);
    }
}
