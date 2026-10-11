namespace BuergerPortal.Web.Features.Maengel.Contracts
{
    /// <summary>Eigene Mängelmeldung, wie die API sie liefert (api/maengelmeldungen/mine).</summary>
    public sealed record MaengelmeldungResponse(Guid Id, DateTime CreatedUtc, string Titel, string Beschreibung,
        double? Latitude, double? Longitude, string? AddressHint, string Status, bool HatFoto);
}
