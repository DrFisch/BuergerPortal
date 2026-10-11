namespace BuergerPortal.Api.Contracts.Maengelmeldungen
{
    /// <summary>
    /// Prüft das Foto einer Mängelmeldung, ohne das Bild zu verarbeiten (keine Bildbibliothek, kleinere Angriffsfläche):
    /// erlaubter Typ, Größe und passende Dateisignatur ("magic bytes") – eine als Bild getarnte andere Datei wird abgelehnt.
    /// Das Portal verkleinert Fotos schon im Browser; dabei fallen Metadaten wie der GPS-Ort aus dem Kamera-EXIF weg.
    /// </summary>
    public static class MangelFotoPruefung
    {
        public const int MaxBytes = 2 * 1024 * 1024;

        public static bool TryDecode(string base64, string? contentType, out byte[] daten, out string fehler)
        {
            daten = [];
            fehler = string.Empty;
            if (contentType is not ("image/jpeg" or "image/png" or "image/webp"))
            {
                fehler = "Bitte ein Foto als JPEG, PNG oder WebP anhängen.";
                return false;
            }
            // Base64 braucht 4 Zeichen je 3 Byte – vor dem Dekodieren grob begrenzen.
            if (base64.Length > (MaxBytes / 3 + 1) * 4)
            {
                fehler = "Das Foto ist zu groß (höchstens 2 MB).";
                return false;
            }
            try
            {
                daten = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                fehler = "Das Foto konnte nicht gelesen werden.";
                return false;
            }
            if (daten.Length == 0 || daten.Length > MaxBytes)
            {
                fehler = "Das Foto ist zu groß (höchstens 2 MB).";
                return false;
            }
            if (!SignaturPasst(daten, contentType))
            {
                fehler = "Die Datei ist kein Foto im angegebenen Format.";
                return false;
            }
            return true;
        }

        private static bool SignaturPasst(byte[] d, string contentType) => contentType switch
        {
            "image/jpeg" => d.Length > 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF,
            "image/png" => d.Length > 8 && d.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => d.Length > 12 && d.AsSpan(0, 4).SequenceEqual("RIFF"u8) && d.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false,
        };
    }
}
