namespace BuergerPortal.Domain.Antrag.Enums
{
    /// <summary>
    /// Art der Meldebescheinigung (§ 18 BMG). Einfach: Name und Anschrift. Erweitert: zusätzlich Geburtsdatum,
    /// Geburtsort und Geburtsname – in der Simulation nur aus den Daten der BundID-Anmeldung, nicht aus einem
    /// Melderegister.
    /// </summary>
    public enum MeldebescheinigungArt
    {
        Einfach = 1,
        Erweitert = 2
    }
}
