using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.Web.Features.Antraege.Reisepass.ViewModels
{
    public sealed class ReisepassStep2Vm
    {
        public Guid Id { get; set; } // Antrag-Id aus Step 1 (Route)

        [Display(Name = "Express-Bearbeitung")]
        public bool Express { get; set; }

        [Display(Name = "Alter Pass vorhanden")]
        public bool AltpassVorhanden { get; set; }

        [MaxLength(1000), Display(Name = "Hinweis (optional)")]
        public string? Hinweis { get; set; }

        // Für die Ansicht: kurze Zusammenfassung aus Step1
        public string? AntragstellerName { get; set; }
        public DateTime? Geburtsdatum { get; set; }
    }
}
