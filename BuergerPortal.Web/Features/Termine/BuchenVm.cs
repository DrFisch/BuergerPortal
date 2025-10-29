using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.Web.Features.Termine
{
    public enum ServiceType { Ausweis, Reisepass, Meldebescheinigung }

    public sealed class BuchenVm
    {
        [Required]
        public ServiceType Service { get; set; }

        [Required, StringLength(200)]
        public string Location { get; set; } = "Bürgeramt Mitte";

        [Required, DataType(DataType.Date)]
        public DateTime LocalDate { get; set; } = DateTime.Today.AddDays(1);

        [Required, DataType(DataType.Time)]
        public TimeSpan LocalTime { get; set; } = NextQuarterHour(DateTime.Now).TimeOfDay;

        [Range(15, 480)]
        public int DurationMinutes { get; set; } = 15;

        private static DateTime NextQuarterHour(DateTime dt)
        {
            var add = 15 - (dt.Minute % 15);
            if (add == 15) add = 0;
            var rounded = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, DateTimeKind.Local)
                          .AddMinutes(add);
            return rounded.AddMinutes(rounded <= dt ? 15 : 0);
        }
    }
}
