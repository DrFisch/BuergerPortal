using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.Web.Features.Antraege.Sperrmuell.ViewModels
{
    public sealed class SperrmuellStep1Vm
    {
        [Required]
        [Display(Name = "Vorname")]
        public string Vorname { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Nachname")]
        public string Nachname { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Geburtsdatum")]
        public DateTime? Geburtsdatum { get; set; }

        [EmailAddress]
        [Display(Name = "E-Mail-Adresse")]
        public string? Email { get; set; }

        [Display(Name = "Telefonnummer")]
        public string? Telefon { get; set; }
    }
}
