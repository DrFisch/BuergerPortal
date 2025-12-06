using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Appointments.DTOs
{
    public sealed class BusySlotDto
    {
        public DateTime StartUtc { get; init; } // UTC
        public DateTime EndUtc { get; init; } // UTC
    }
}
