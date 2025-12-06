using BuergerPortal.Domain.Appointments.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Appointments.DTOs
{
    public sealed class AppointmentCreateDto
    {
        public ServiceType Service { get; init; }
        public LocationType Location { get; init; } 
        public DateTime StartUtc { get; init; }
        public DateTime EndUtc { get; init; }
        public Guid? AntragId { get; set; }
    }
}
