using BuergerPortal.Application.Poi.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Interfaces.BusinessServices
{
    public interface IPoiBusinessService
    {
        Task<List<PoiDto>> GetDiscoveryListAsync(CancellationToken ct);
    }
}
