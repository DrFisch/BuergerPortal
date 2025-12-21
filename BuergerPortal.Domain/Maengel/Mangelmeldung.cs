using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Domain.Maengel
{
    public sealed class Maengelmeldung
    {
        public Guid Id { get; set; }

        public Guid? ReporterUserId { get; set; }   // optional
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public string Titel { get; set; } = default!;
        public string Beschreibung { get; set; } = default!;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? AddressHint { get; set; }

        public string Status { get; set; } = "Offen";
    }

}
