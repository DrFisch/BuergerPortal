using BuergerPortal.Domain.Appointments.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Appointments.DTOs
{
    public sealed class AppointmentSummaryDto
    {
        public Guid Id { get; set; }
        public ServiceType Service { get; set; }
        public string Location { get; set; } = "";
        public DateTime StartUtc { get; set; } // UTC
        public DateTime EndUtc { get; set; }   // UTC
        public bool Cancelled { get; set; }
    }
}
