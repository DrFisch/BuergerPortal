using BuergerPortal.Domain.Appointments.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Domain.Appointments.Entity
{
    public sealed class Appointment
    {
        public Guid Id { get; set; }
        public ServiceType Service { get; set; }
        public LocationType Location { get; set; } = LocationType.BuergermtMitte;
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public Guid UserId { get; set; } = default!;
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;

        //Antrag
        public Guid? AntragId { get; set; }          


        // Concurrency
        public byte[] RowVersion { get; set; } = default!;
    }
}
