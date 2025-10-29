using BuergerPortal.Domain.Appointments;

namespace BuergerPortal.Api.Contracts.Appointments
{
    public sealed class AppointmentListItemResponse
    {
        public Guid Id { get; set; }
        public ServiceType Service { get; set; }
        public string Location { get; set; } = "";
        public DateTime StartUtc { get; set; } // UTC (Kind=Utc)
        public DateTime EndUtc { get; set; }   // UTC (Kind=Utc)
        public bool Cancelled { get; set; }
    }
}
