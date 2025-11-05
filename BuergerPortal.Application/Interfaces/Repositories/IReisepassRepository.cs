using BuergerPortal.Domain.Antrag.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Interfaces.Repositories
{
    public interface IReisepassRepository
    {
        Task<ReisepassAntrag?> GetAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<ReisepassAntrag>> GetAllForUserAsync(Guid userId, CancellationToken ct);

        // Diese Methoden übernehmen das Speichern (SaveChanges) intern:
        Task AddAsync(ReisepassAntrag entity, CancellationToken ct);
        Task UpdateAsync(ReisepassAntrag entity, CancellationToken ct);

        Task<bool> ExistsForUserAsync(Guid id, Guid userId, CancellationToken ct);
    }
}
