using BuergerPortal.Domain.Antrag.Entity;

namespace BuergerPortal.Application.Interfaces.Repositories
{
    public interface IMeldebescheinigungRepository
    {
        Task<MeldebescheinigungAntrag?> GetAsync(Guid id, CancellationToken ct);
        Task<IReadOnlyList<MeldebescheinigungAntrag>> GetAllForUserAsync(Guid userId, CancellationToken ct);

        // speichern selbst (SaveChanges)
        Task AddAsync(MeldebescheinigungAntrag entity, CancellationToken ct);
        Task UpdateAsync(MeldebescheinigungAntrag entity, CancellationToken ct);
    }
}
