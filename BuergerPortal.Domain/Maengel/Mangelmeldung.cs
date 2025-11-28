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

        public Guid? ReporterUserId { get; set; }   // optional, falls anonym möglich
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public string Titel { get; set; } = default!;
        public string Beschreibung { get; set; } = default!;

        // Location
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? AddressHint { get; set; }

        // Status
        public string Status { get; set; } = "Offen";   // oder Enum

        // Optional: nur Pfad/Id in externem Storage
        public string? PhotoPath { get; set; }
    }

}
