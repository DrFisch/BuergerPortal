using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.Web.Features.Antraege.Sperrmuell.ViewModels
{
    public sealed class SperrmuellStep2Vm
    {
        public Guid Id { get; set; }

        public string AntragstellerName { get; set; } = string.Empty;
        public DateTime? Geburtsdatum { get; set; }

        /// <summary>
        /// Die nächsten Werktage ab dem Tag nach <paramref name="heute"/> – als Tages-Chips für den Abholtag
        /// (2026-10-18). Wie die Prüfung im Controller nur Montag bis Freitag; Feiertage prüft die Simulation nicht.
        /// </summary>
        public static IReadOnlyList<DateOnly> NaechsteWerktage(DateOnly heute, int anzahl)
        {
            var tage = new List<DateOnly>(anzahl);
            for (var tag = heute.AddDays(1); tage.Count < anzahl; tag = tag.AddDays(1))
            {
                if (tag.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                {
                    tage.Add(tag);
                }
            }
            return tage;
        }

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
        [Display(Name = "Wunschdatum")]
        [DataType(DataType.DateTime)]
        public DateTime? Wunschzeit { get; set; }

        [Display(Name = "Hinweis für das Entsorgungsteam")]
        public string? Hinweis { get; set; }
    }
}
