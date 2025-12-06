using BuergerPortal.Web.Features.Termine.ViewModels;

namespace BuergerPortal.Web.Features.Termine.Contracts
{
    public sealed class AppointmentListItemResponse
    {
        public Guid Id { get; set; }
        public ServiceType Service { get; set; }       
        public LocationType Location { get; set; } 
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public bool Cancelled { get; set; }
        public Guid? AntragId { get; set; }
    }
}
