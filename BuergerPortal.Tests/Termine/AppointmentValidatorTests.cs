using BuergerPortal.Application.Appointments.DTOs;
using BuergerPortal.Application.Appointments.Validation;
using BuergerPortal.Domain.Appointments.Enums;

namespace BuergerPortal.Tests.Termine
{
    /// <summary>Fachliche Prüfung einer Buchung (Geschäftszeiten, Feiertage, Raster, Dauer, Vorlauf).</summary>
    public class AppointmentValidatorTests
    {
        /// <summary>Feste Uhr: Mittwoch, 07.10.2026, 10:00 Uhr Ortszeit (08:00 UTC).</summary>
        public sealed class FixedTime(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private static readonly FixedTime Now = new(new DateTimeOffset(2026, 10, 7, 8, 0, 0, TimeSpan.Zero));

        // Ortszeit (Sommerzeit, UTC+2) → UTC
        private static AppointmentCreateDto Termin(string datum, string uhrzeit, int minuten = 15)
        {
            var local = DateTime.Parse($"{datum}T{uhrzeit}:00");
            var startUtc = DateTime.SpecifyKind(local.AddHours(local.Month is >= 4 and <= 10 ? -2 : -1), DateTimeKind.Utc);
            return new AppointmentCreateDto
            {
                Service = ServiceType.AllgemeineBeratung,
                Location = LocationType.BuergermtMitte,
                StartUtc = startUtc,
                EndUtc = startUtc.AddMinutes(minuten),
            };
        }

        private static List<string> Fehler(AppointmentCreateDto dto) =>
            new AppointmentCreateDtoValidator(Now).Validate(dto).Errors.Select(e => e.ErrorMessage).ToList();

        [Fact]
        public void Werktag_in_der_Geschaeftszeit_ist_gueltig() =>
            Assert.Empty(Fehler(Termin("2026-10-08", "09:30", 30)));

        [Theory]
        [InlineData("2026-12-25", "Am 25.12.2026 ist Feiertag (1. Weihnachtstag)")]
        [InlineData("2027-01-01", "Am 01.01.2027 ist Feiertag (Neujahr)")]
        public void Feiertag_unter_der_Woche_wird_abgelehnt(string datum, string meldung)
        {
            var fehler = Fehler(Termin(datum, "09:00"));
            Assert.Single(fehler);
            Assert.StartsWith(meldung, fehler[0]);
        }

        [Fact]
        public void Mehr_als_90_Tage_im_Voraus_wird_abgelehnt()
        {
            Assert.Contains(Fehler(Termin("2027-01-07", "09:00")), f => f.Contains("90 Tage"));
            Assert.DoesNotContain(Fehler(Termin("2027-01-05", "09:00")), f => f.Contains("90 Tage"));
        }

        [Fact]
        public void Vergangene_Uhrzeit_heute_wird_abgelehnt() =>
            Assert.Contains("Datum liegt in der Vergangenheit.", Fehler(Termin("2026-10-07", "09:45")));

        [Fact]
        public void Wochenende_und_Geschaeftsschluss_werden_abgelehnt()
        {
            Assert.Contains("Nur Montag–Freitag 08:00–12:00.", Fehler(Termin("2026-10-10", "09:00")));
            Assert.Contains("Termin liegt außerhalb der Geschäftszeit (08:00–12:00).",
                Fehler(Termin("2026-10-08", "11:45", 30)));
        }

        [Fact]
        public void Nur_15_30_oder_45_Minuten()
        {
            Assert.Contains("Ein Termin dauert 15, 30 oder 45 Minuten.", Fehler(Termin("2026-10-08", "08:00", 60)));
            Assert.Empty(Fehler(Termin("2026-10-08", "08:00", 45)));
        }
    }
}
