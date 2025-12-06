using BuergerPortal.Domain.Antrag.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Interfaces.Repositories
{
    public interface ISperrmuellRepository
    {
        Task<SperrmuellAntrag?> GetAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<SperrmuellAntrag>> GetAllForUserAsync(Guid userId, CancellationToken ct);

        // Diese Methoden übernehmen das Speichern (SaveChanges) intern:
        Task AddAsync(SperrmuellAntrag entity, CancellationToken ct);
        Task UpdateAsync(SperrmuellAntrag entity, CancellationToken ct);

        Task<bool> ExistsForUserAsync(Guid id, Guid userId, CancellationToken ct);
    }
}
