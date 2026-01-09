using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Application.Poi.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Poi.BusinessService
{
    public sealed class PoiBusinessService : IPoiBusinessService
    {
        private readonly IPoiRepository _repo;

        public PoiBusinessService(IPoiRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<PoiDto>> GetDiscoveryListAsync(CancellationToken ct)
        {
            var list = await _repo.GetAllActiveAsync(ct);

            return list.Select(x => new PoiDto
            {
                Id = x.Id,
                Name = x.Name,
                Category = x.Category.ToString(), // Enum zu String für Frontend Filter
                Description = x.Description,
                Tags = x.Tags,
                Icon = x.Icon,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                Address = x.Address
            }).ToList();
        }

        
    }
}
