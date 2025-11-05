using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.Web.Features.Antraege.Reisepass.ViewModels
{
    public sealed class ReisepassStep1Vm
    {
        [Required, Display(Name = "Vorname")]
        public string Vorname { get; set; } = string.Empty;

        [Required, Display(Name = "Nachname")]
        public string Nachname { get; set; } = string.Empty;

        [Required, DataType(DataType.Date), Display(Name = "Geburtsdatum")]
        public DateTime? Geburtsdatum { get; set; } // HTML date -> DateTime; wir mappen zu DateOnly

        [EmailAddress, Display(Name = "E-Mail (optional)")]
        public string? Email { get; set; }

        [Phone, Display(Name = "Telefon (optional)")]
        public string? Telefon { get; set; }
    }
}
