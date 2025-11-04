using BuergerPortal.Domain.Antrag.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Interfaces.Repositories
{
    public interface IAntragRepository
    {
        Task<ReisepassAntrag?> GetReisepassAntragByIdAsync(Guid id, CancellationToken ct= default);
        Task AddReisepassAsync(ReisepassAntrag entity, CancellationToken ct= default);
        Task UpdateAsync(Antrag entity, CancellationToken ct = default);

        Task<IReadOnlyList<Antrag>> GetAllByApplicantUserIdAsync(Guid applicantUserId, CancellationToken ct = default);
        Task<bool> ExistsAsync(Guid id, string userId, CancellationToken ct = default);
    }
}
