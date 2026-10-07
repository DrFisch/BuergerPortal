using BuergerPortal.Application.Appointments.Calendar;

namespace BuergerPortal.Tests.Termine
{
    /// <summary>Feiertage in Bayern: an diesen Tagen sind keine Termine buchbar.</summary>
    public class BayerischeFeiertageTests
    {
        [Theory]
        [InlineData(2026, 4, 5)]
        [InlineData(2027, 3, 28)]
        [InlineData(2025, 4, 20)]
        [InlineData(2024, 3, 31)]
        public void Berechnet_Ostersonntag(int year, int month, int day) =>
            Assert.Equal(new DateOnly(year, month, day), BayerischeFeiertage.Ostersonntag(year));

        [Theory]
        [InlineData("2026-01-01", "Neujahr")]
        [InlineData("2026-01-06", "Heilige Drei Könige")]
        [InlineData("2026-04-03", "Karfreitag")]
        [InlineData("2026-04-06", "Ostermontag")]
        [InlineData("2026-05-01", "Tag der Arbeit")]
        [InlineData("2026-05-14", "Christi Himmelfahrt")]
        [InlineData("2026-05-25", "Pfingstmontag")]
        [InlineData("2026-06-04", "Fronleichnam")]
        [InlineData("2026-10-03", "Tag der Deutschen Einheit")]
        [InlineData("2026-11-01", "Allerheiligen")]
        [InlineData("2026-12-25", "1. Weihnachtstag")]
        [InlineData("2026-12-26", "2. Weihnachtstag")]
        public void Erkennt_Feiertage_2026(string date, string name) =>
            Assert.Equal(name, BayerischeFeiertage.Name(DateOnly.Parse(date)));

        [Theory]
        [InlineData("2026-10-08")]
        [InlineData("2026-08-15")] // Mariä Himmelfahrt: nur in überwiegend katholischen Gemeinden, hier nicht
        [InlineData("2026-11-18")] // Buß- und Bettag: in Bayern kein gesetzlicher Feiertag
        [InlineData("2026-12-24")]
        public void Normale_Tage_sind_keine_Feiertage(string date) =>
            Assert.False(BayerischeFeiertage.IsFeiertag(DateOnly.Parse(date)));
    }
}
