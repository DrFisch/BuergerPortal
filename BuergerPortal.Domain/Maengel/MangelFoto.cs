namespace BuergerPortal.Domain.Maengel
{
    /// <summary>
    /// Foto zu einer Mängelmeldung (aus der Kamera oder der Galerie, im Browser verkleinert). Eigene Tabelle, damit Listen
    /// der Meldungen die Bilddaten nicht mitladen.
    /// </summary>
    public sealed class MangelFoto
    {
        public Guid MaengelmeldungId { get; set; }
        public string ContentType { get; set; } = "image/jpeg";
        public byte[] Daten { get; set; } = [];
    }
}
