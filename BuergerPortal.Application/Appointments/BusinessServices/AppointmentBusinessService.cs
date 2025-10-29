using BuergerPortal.Application.Appointments.DTOs;
using BuergerPortal.Application.Common;
using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Appointments;
using BuergerPortal.Domain.Appointments.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Appointments.BusinessServices
{
    public sealed class AppointmentBusinessService : IAppointmentBusinessService
    {
        private readonly IAppointmentRepository _repo;

        public AppointmentBusinessService(IAppointmentRepository repo)
        {
            _repo = repo;
        }

        public async Task<Result<Guid>> BookAsync(AppointmentCreateDto dto, string currentUserId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(currentUserId) || dto.StartUtc >= dto.EndUtc)
            {
                return Result<Guid>.Fail(ErrorCodes.Validation, "Ungültige Eingaben.");
            }

            if (dto.StartUtc < DateTime.UtcNow.AddMinutes(-1))
            {
                return Result<Guid>.Fail(ErrorCodes.Validation, "Datum liegt in der Vergangenheit.");
            }

            var collides = await _repo.ExistsOverlapAsync(currentUserId, dto.StartUtc, dto.EndUtc, ct);
            if (collides)
            {
                return Result<Guid>.Fail(ErrorCodes.SlotConflict, "Zeitslot bereits belegt.");
            }

            var entity = new Appointment
            {
                Id = Guid.NewGuid(),
                Service = dto.Service,
                Location = dto.Location,
                StartUtc = dto.StartUtc,
                EndUtc = dto.EndUtc,
                UserId = currentUserId,
                Status = AppointmentStatus.Booked
            };

            await _repo.CreateAsync(entity, ct);
            return Result<Guid>.Success(entity.Id);
        }
    }
}
