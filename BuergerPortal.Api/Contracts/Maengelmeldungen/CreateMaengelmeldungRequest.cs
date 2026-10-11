using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.Api.Contracts.Maengelmeldungen
{
    public sealed class CreateMaengelmeldungRequest
    {
        [Required, MaxLength(200)]
        public string Titel { get; set; } = default!;

        [Required, MaxLength(4000)]
        public string Beschreibung { get; set; } = default!;

        [Required(ErrorMessage = "Latitude darf nicht null sein.")]
        public double? Latitude { get; set; }

        [Required(ErrorMessage = "Longitude darf nicht null sein.")]
        public double? Longitude { get; set; }

        [MaxLength(500)]
        public string? AddressHint { get; set; }

        // Optional: Foto (Base64) mit Typ image/jpeg, image/png oder image/webp, höchstens 2 MB (MangelFotoPruefung)
        public string? FotoBase64 { get; set; }
        public string? FotoContentType { get; set; }
    }
}
