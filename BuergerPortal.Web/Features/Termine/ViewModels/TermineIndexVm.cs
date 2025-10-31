namespace BuergerPortal.Web.Features.Termine.ViewModels
{
    public sealed class TermineIndexVm
    {
        public List<TerminListItemVm> Termine { get; set; } = new();
    }

    public sealed class TerminListItemVm
    {
        public Guid Id { get; set; }
        public string Dienst { get; set; } = "";
        public DateTime Datum { get; set; }    // nur Datum nutzen wir in der View
        public string Uhrzeit { get; set; } = ""; // "HH:mm"
        public LocationType Location { get; set; } 
        public bool Storniert { get; set; }
    }
}
