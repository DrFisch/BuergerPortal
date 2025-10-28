using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Domain.Appointments
{
    public sealed class Appointment
    {
        public Guid Id { get; set; }
        public ServiceType Service { get; set; }
        public string Location { get; set; } = "Bürgeramt Mitte";
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public string UserId { get; set; } = default!;
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;

        // Concurrency
        public byte[] RowVersion { get; set; } = default!;
    }
}
