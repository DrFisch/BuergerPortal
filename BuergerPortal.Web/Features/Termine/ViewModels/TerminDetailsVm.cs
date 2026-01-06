using BuergerPortal.Web.Extensions;

namespace BuergerPortal.Web.Features.Termine.ViewModels
{
    public class TerminDetailsVm
    {
        public Guid Id { get; set; }
        public ServiceType Service { get; set; }
        public DateTime Datum { get; set; }
        public string Uhrzeit { get; set; } = ""; // "HH:mm"
        public LocationType Location { get; set; }
        public bool Storniert { get; set; }
        public Guid? AntragId { get; set; }

        // Hilfseigenschaft für die Anzeige in der View
        public string ServiceName => Service.ToDisplayName();
    }
}
