namespace BuergerPortal.Web.Features.Maengel.ViewModels
{
    public class CreateMangelVm
    {
        public string Titel { get; set; } = string.Empty;
        public string Beschreibung { get; set; } = string.Empty;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
