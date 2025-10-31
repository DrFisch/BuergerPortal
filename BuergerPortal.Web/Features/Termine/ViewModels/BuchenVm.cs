using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.Web.Features.Termine.ViewModels
{
    public enum ServiceType
    {
        [Display(Name = "Allgemeine Beratung")]
        AllgemeineBeratung = 1,

        [Display(Name = "Dokumente und Ausweise")]
        DokumenteUndAusweise = 2,

        [Display(Name = "Meldeangelegenheiten")]
        Meldeangelegenheiten = 3,

        [Display(Name = "Sonstiges")]
        Sonstiges = 4
    }

    public enum LocationType
    {
        [Display(Name = "Bürgeramt Mitte")] BuergermtMitte = 1,
        [Display(Name = "Bürgeramt Nord")] BuergermtNord = 2,
        [Display(Name = "Bürgeramt Süd")] BuergermtSued = 3
    }

    public enum DurationOption
    {
        [Display(Name = "15 Minuten")] Min15 = 15,
        [Display(Name = "30 Minuten")] Min30 = 30,
        [Display(Name = "45 Minuten")] Min45 = 45
    }

    public sealed class BuchenVm
    {
        [Required]
        public ServiceType Service { get; set; }

        [Required]  // jetzt Enum statt string
        public LocationType Location { get; set; } = LocationType.BuergermtMitte;

        [Required, DataType(DataType.Date)]
        public DateTime LocalDate { get; set; } = DateTime.Today.AddDays(1);

        [Required, DataType(DataType.Time)]
        public TimeSpan LocalTime { get; set; } = NextQuarterHour(DateTime.Now).TimeOfDay;

        [Required]
        [EnumDataType(typeof(DurationOption))]
        public DurationOption Duration { get; set; } = DurationOption.Min15;

        // Für die API: Minutenwert aus dem Enum
        public int DurationMinutes => (int)Duration;

        private static DateTime NextQuarterHour(DateTime dt)
        {
            var add = 15 - dt.Minute % 15;
            if (add == 15) add = 0;
            var rounded = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, DateTimeKind.Local)
                          .AddMinutes(add);
            return rounded.AddMinutes(rounded <= dt ? 15 : 0);
        }
    }
}
