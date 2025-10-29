using BuergerPortal.Domain.Appointments;
using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.Api.Contracts
{
    public sealed class AppointmentCreateRequest
    {
        [Required]
        public ServiceType Service { get; init; }

        [Required, StringLength(200)]
        public string Location { get; init; } = "";

        // Erwartet UTC-Zeiten
        [Required]
        public DateTime StartUtc { get; init; }

        [Required]
        public DateTime EndUtc { get; init; }
    }
}
