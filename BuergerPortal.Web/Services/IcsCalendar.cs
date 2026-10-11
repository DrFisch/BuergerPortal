using System.Globalization;
using System.Text;

namespace BuergerPortal.Web.Services
{
    /// <summary>Ein Termin für die Kalenderdatei. Zeiten in UTC.</summary>
    public sealed record IcsEvent(string Uid, DateTime StartUtc, DateTime EndUtc, string Summary,
        string? Location = null, string? Description = null, string? Url = null, TimeSpan? Reminder = null);

    /// <summary>
    /// Erzeugt eine Kalenderdatei (iCalendar, RFC 5545) mit genau einem Termin – „Zum Kalender hinzufügen“ funktioniert so
    /// mit jedem Kalender (iPhone, Android, Outlook, Thunderbird), ohne dass Termindaten an einen fremden Dienst gehen.
    /// Zeilen enden mit CRLF, lange Zeilen werden nach 75 Byte gefaltet, Texte maskiert (§3.1, §3.3.11).
    /// </summary>
    public static class IcsCalendar
    {
        public const string ContentType = "text/calendar; charset=utf-8";

        public static string Create(IcsEvent ev, DateTime nowUtc)
        {
            var lines = new List<string>
            {
                "BEGIN:VCALENDAR",
                "VERSION:2.0",
                "PRODID:-//BuergerPortal (Studienprojekt)//Termine//DE",
                "CALSCALE:GREGORIAN",
                "METHOD:PUBLISH",
                "BEGIN:VEVENT",
                "UID:" + Escape(ev.Uid),
                "DTSTAMP:" + Utc(nowUtc),
                "DTSTART:" + Utc(ev.StartUtc),
                "DTEND:" + Utc(ev.EndUtc),
                "SUMMARY:" + Escape(ev.Summary),
            };
            if (!string.IsNullOrWhiteSpace(ev.Location)) lines.Add("LOCATION:" + Escape(ev.Location));
            if (!string.IsNullOrWhiteSpace(ev.Description)) lines.Add("DESCRIPTION:" + Escape(ev.Description));
            if (!string.IsNullOrWhiteSpace(ev.Url)) lines.Add("URL:" + ev.Url);
            lines.Add("STATUS:CONFIRMED");
            if (ev.Reminder is { } reminder)
            {
                lines.AddRange(
                [
                    "BEGIN:VALARM",
                    "ACTION:DISPLAY",
                    "TRIGGER:-PT" + (int)reminder.TotalMinutes + "M",
                    "DESCRIPTION:" + Escape(ev.Summary),
                    "END:VALARM",
                ]);
            }
            lines.AddRange(["END:VEVENT", "END:VCALENDAR"]);

            var builder = new StringBuilder();
            foreach (var line in lines) builder.Append(Fold(line)).Append("\r\n");
            return builder.ToString();
        }

        // TEXT-Werte: Backslash, Semikolon, Komma und Zeilenumbruch maskieren (RFC 5545 §3.3.11).
        public static string Escape(string text) => text
            .Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
            .Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");

        // Zeilen länger als 75 Byte (UTF-8) falten: CRLF + Leerzeichen; Umlaute werden dabei nicht zerteilt (§3.1).
        public static string Fold(string line)
        {
            var builder = new StringBuilder();
            const int limit = 75;
            var bytes = 0;
            foreach (var rune in line.EnumerateRunes())
            {
                var size = rune.Utf8SequenceLength;
                if (bytes + size > limit)
                {
                    builder.Append("\r\n ");
                    bytes = 1;          // das Leerzeichen zählt mit
                }
                builder.Append(rune.ToString());
                bytes += size;
            }
            return builder.ToString();
        }

        private static string Utc(DateTime value) =>
            (value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc))
            .ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
    }
}
