using BuergerPortal.Web.Features.Antraege.Reisepass;

namespace BuergerPortal.Web.Features.Termine.ViewModels
{
    public sealed class TermineIndexVm
    {
        public List<TerminListItemVm> Termine { get; set; } = new();
    }

    public sealed class TerminListItemVm
    {
        public Guid Id { get; set; }
        public ServiceType Service { get; set; }
        public DateTime Datum { get; set; }    
        public string Uhrzeit { get; set; } = ""; // "HH:mm–HH:mm"

        /// <summary>Beginn in Ortszeit (Europe/Berlin).</summary>
        public DateTime Start { get; set; }

        /// <summary>Termin hat schon begonnen (Vergleich in Ortszeit, nicht mit der Uhr des Servers).</summary>
        public bool Vergangen { get; set; }
        public LocationType Location { get; set; } 
        public bool Storniert { get; set; }

        public Guid? AntragId { get; set; }
        
    }
}
