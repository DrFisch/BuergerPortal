using BuergerPortal.Domain.Poi.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Interfaces.Repositories
{
    public interface IPoiRepository
    {
        Task<List<PoiEntity>> GetAllActiveAsync(CancellationToken ct);
        Task<PoiEntity?> GetByIdAsync(Guid id, CancellationToken ct);
    }
}
