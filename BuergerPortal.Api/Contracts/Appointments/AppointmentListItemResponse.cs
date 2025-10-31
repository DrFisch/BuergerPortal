using BuergerPortal.Domain.Appointments.Enums;

namespace BuergerPortal.Api.Contracts.Appointments
{
    public sealed class AppointmentListItemResponse
    {
        public Guid Id { get; set; }
        public ServiceType Service { get; set; }
        public LocationType Location { get; set; } = LocationType.BuergermtMitte;
        public DateTime StartUtc { get; set; } // UTC (Kind=Utc)
        public DateTime EndUtc { get; set; }   // UTC (Kind=Utc)
        public bool Cancelled { get; set; }
    }
}
