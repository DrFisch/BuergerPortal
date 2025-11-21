using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.Web.Features.Antraege.Sperrmuell.ViewModels
{
    public sealed class SperrmuellStep2Vm
    {
        public Guid Id { get; set; }

        public string AntragstellerName { get; set; } = string.Empty;
        public DateTime? Geburtsdatum { get; set; }

        [Required]
        [Display(Name = "Straße")]
        public string Strasse { get; set; } = string.Empty;

        [Required]
        [Display(Name = "PLZ")]
        public string PLZ { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Ort")]
        public string Ort { get; set; } = string.Empty;

        [Display(Name = "Holz (m³)")]
        public int? HolzKubikmeter { get; set; }

        [Display(Name = "Sonstiges (m³)")]
        public int? SonstigesKubikmeter { get; set; }

        [Display(Name = "Matratzen (Anzahl)")]
        public int? Matratzen { get; set; }

        [Required]
        [Display(Name = "Wunschzeit")]
        [DataType(DataType.DateTime)]
        public DateTime? Wunschzeit { get; set; }

        [Display(Name = "Hinweis für das Entsorgungsteam")]
        public string? Hinweis { get; set; }
    }
}
