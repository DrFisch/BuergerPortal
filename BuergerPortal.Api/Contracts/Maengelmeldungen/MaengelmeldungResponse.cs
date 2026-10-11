namespace BuergerPortal.Api.Contracts.Maengelmeldungen
{
    /// <summary>Eine eigene Mängelmeldung (ohne Melder-ID und ohne Bilddaten; das Foto gibt es unter …/{id}/foto).</summary>
    public sealed record MaengelmeldungResponse(Guid Id, DateTime CreatedUtc, string Titel, string Beschreibung,
        double? Latitude, double? Longitude, string? AddressHint, string Status, bool HatFoto);
}
