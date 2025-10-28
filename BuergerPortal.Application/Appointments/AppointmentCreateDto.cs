using BuergerPortal.Domain.Appointments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Appointments
{
    public sealed class AppointmentCreateDto
    {
        public ServiceType Service { get; init; }
        public string Location { get; init; } = "";
        public DateTime StartUtc { get; init; }
        public DateTime EndUtc { get; init; }
        // UserId kommt NICHT vom Client – siehe BusinessService
    }
}
