using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.Api.Contracts.Maengelmeldungen
{
    public sealed class CreateMaengelmeldungRequest
    {
        [Required, MaxLength(200)]
        public string Titel { get; set; } = default!;

        [Required, MaxLength(4000)]
        public string Beschreibung { get; set; } = default!;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [MaxLength(500)]
        public string? AddressHint { get; set; }
    }
}
