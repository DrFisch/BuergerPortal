using BuergerPortal.Domain.Poi.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Domain.Poi.Entity
{
    public sealed class PoiEntity
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public PoiCategory Category { get; set; }

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Suchbegriffe durch Leerzeichen getrennt für die Client-seitige Suche
        /// </summary>
        public string Tags { get; set; } = string.Empty;

        /// <summary>
        /// Bootstrap Icon Klasse (z.B. bi-building)
        /// </summary>
        public string Icon { get; set; } = "bi-geo-alt";

        // Koordinaten für Karten-Integration
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;

        // Concurrency (wie bei deinen anderen Entities)
        public byte[] RowVersion { get; set; } = default!;
    }
}
