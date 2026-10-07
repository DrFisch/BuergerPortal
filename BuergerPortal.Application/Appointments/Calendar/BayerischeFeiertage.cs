namespace BuergerPortal.Application.Appointments.Calendar
{
    /// <summary>
    /// Gesetzliche Feiertage in Bayern nach Art. 1 Abs. 1 des Bayerischen Feiertagsgesetzes (FTG): An diesen Tagen sind
    /// die Bürgerämter geschlossen, Termine also nicht buchbar.
    /// Nicht enthalten: Mariä Himmelfahrt (15.08.) gilt nur in Gemeinden mit überwiegend katholischer Bevölkerung und
    /// das Augsburger Friedensfest (08.08.) nur in Augsburg – für die Beispielstadt des Portals wird beides nicht
    /// berücksichtigt (Annahme).
    /// </summary>
    public static class BayerischeFeiertage
    {
        /// <summary>Name des Feiertags oder null, wenn der Tag kein Feiertag ist.</summary>
        public static string? Name(DateOnly date)
        {
            var fest = (date.Month, date.Day) switch
            {
                (1, 1) => "Neujahr",
                (1, 6) => "Heilige Drei Könige",
                (5, 1) => "Tag der Arbeit",
                (10, 3) => "Tag der Deutschen Einheit",
                (11, 1) => "Allerheiligen",
                (12, 25) => "1. Weihnachtstag",
                (12, 26) => "2. Weihnachtstag",
                _ => null
            };
            if (fest is not null)
            {
                return fest;
            }

            // Bewegliche Feiertage hängen am Ostersonntag.
            return (date.DayNumber - Ostersonntag(date.Year).DayNumber) switch
            {
                -2 => "Karfreitag",
                1 => "Ostermontag",
                39 => "Christi Himmelfahrt",
                50 => "Pfingstmontag",
                60 => "Fronleichnam",
                _ => null
            };
        }

        public static bool IsFeiertag(DateOnly date) => Name(date) is not null;

        /// <summary>Ostersonntag im gregorianischen Kalender (Algorithmus nach Meeus/Jones/Butcher).</summary>
        public static DateOnly Ostersonntag(int year)
        {
            var a = year % 19;
            var b = year / 100;
            var c = year % 100;
            var d = b / 4;
            var e = b % 4;
            var f = (b + 8) / 25;
            var g = (b - f + 1) / 3;
            var h = (19 * a + b - d - g + 15) % 30;
            var i = c / 4;
            var k = c % 4;
            var l = (32 + 2 * e + 2 * i - h - k) % 7;
            var m = (a + 11 * h + 22 * l) / 451;
            var month = (h + l - 7 * m + 114) / 31;
            var day = (h + l - 7 * m + 114) % 31 + 1;
            return new DateOnly(year, month, day);
        }
    }
}
