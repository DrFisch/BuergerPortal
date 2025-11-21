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
        Task<IReadOnlyList<Antrag>> GetAllForUserAsync(Guid userId, CancellationToken ct);
    }
}
