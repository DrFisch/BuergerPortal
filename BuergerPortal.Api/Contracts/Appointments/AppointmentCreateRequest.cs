using BuergerPortal.Domain.Appointments.Enums;
using System.ComponentModel.DataAnnotations;

namespace BuergerPortal.Api.Contracts.Appointments
{
    public sealed class AppointmentCreateRequest
    {
        [Required]
        public ServiceType Service { get; init; }

        [Required]
        public LocationType Location { get; init; }

        // Erwartet UTC-Zeiten
        [Required]
        public DateTime StartUtc { get; init; }

        [Required]
        public DateTime EndUtc { get; init; }

        // <-- nullable, damit null / fehlendes Feld nicht zu ModelState-Fehlern führt
        public Guid? AntragId { get; init; }
    }
}
