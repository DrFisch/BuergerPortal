using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.Web.Features.Maengel.ViewModels
{
    public sealed class CreateMangelVm
    {
        [Required(ErrorMessage = "Bitte geben Sie einen Titel an.")]
        [MaxLength(200, ErrorMessage = "Der Titel darf maximal 200 Zeichen lang sein.")]
        public string Titel { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte beschreiben Sie den Mangel.")]
        [MaxLength(4000, ErrorMessage = "Die Beschreibung darf maximal 4000 Zeichen lang sein.")]
        public string Beschreibung { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte markieren Sie den Standort auf der Karte.")]
        public double? Latitude { get; set; }

        [Required(ErrorMessage = "Bitte markieren Sie den Standort auf der Karte.")]
        public double? Longitude { get; set; }

        [MaxLength(500, ErrorMessage = "Der Adresshinweis darf maximal 500 Zeichen lang sein.")]
        public string? AddressHint { get; set; }
    }
}
