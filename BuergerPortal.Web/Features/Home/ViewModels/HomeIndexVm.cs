using BuergerPortal.Web.Features.Termine.Contracts;
using BuergerPortal.Web.Features.Termine.ViewModels;

namespace BuergerPortal.Web.Features.Home.ViewModels
{
    public sealed class HomeIndexVm
    {
        public NextAppointmentVm? NextAppointment { get; set; }
        public WeatherVm? Weather { get; set; }
    }

    public sealed class NextAppointmentVm
    {
        public Guid Id { get; set; }
        public ServiceType Service { get; set; }
        public DateTime Datum { get; set; }
        public string Uhrzeit { get; set; } = ""; // "HH:mm"
        public LocationType Location { get; set; }
        public bool Storniert { get; set; }

        public Guid? AntragId { get; set; }

    }
    public class WeatherVm
    {
        public double Temperature { get; set; }
        public string? Condition { get; set; }
        public string? IconClass { get; set; }
    }
}
