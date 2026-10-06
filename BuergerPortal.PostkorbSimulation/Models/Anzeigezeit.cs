namespace BuergerPortal.PostkorbSimulation.Models;

/// <summary>Zeitpunkte werden als UTC gespeichert und für die Anzeige in deutsche Ortszeit umgerechnet.</summary>
public static class Anzeigezeit
{
    private static readonly TimeZoneInfo Berlin = FindBerlin();

    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Berlin);

    // IANA-Name funktioniert unter Linux und unter Windows (ICU); ohne Zeitzonendaten bleibt es bei UTC.
    private static TimeZoneInfo FindBerlin()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
