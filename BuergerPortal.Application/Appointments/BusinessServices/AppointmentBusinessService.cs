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

        public async Task<List<AppointmentListItemDto>> GetAllForUserAsync(string userId, CancellationToken ct)
        {
            var list = await _repo.GetAllForUserAsync(userId, ct);

            return list
                .OrderBy(x => x.StartUtc)
                .Select(x => new AppointmentListItemDto
                {
                    Id = x.Id,
                    Service = x.Service,
                    Location = x.Location,
                    // Wichtig: als UTC markieren, damit im JSON ein „Z“ steht
                    StartUtc = DateTime.SpecifyKind(x.StartUtc, DateTimeKind.Utc),
                    EndUtc = DateTime.SpecifyKind(x.EndUtc, DateTimeKind.Utc),
                    Cancelled = x.Status == AppointmentStatus.Cancelled
                })
                .ToList();
        }
        public async Task<List<BusySlotDto>> GetBusyAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            // Guard
            if (fromUtc.Kind != DateTimeKind.Utc) fromUtc = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
            if (toUtc.Kind != DateTimeKind.Utc) toUtc = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc);
            if (toUtc <= fromUtc) return new List<BusySlotDto>();

            var overlaps = await _repo.GetOverlappingAsync(fromUtc, toUtc, ct);
            return overlaps
                .Select(a => new BusySlotDto
                {
                    StartUtc = DateTime.SpecifyKind(a.StartUtc, DateTimeKind.Utc),
                    EndUtc = DateTime.SpecifyKind(a.EndUtc, DateTimeKind.Utc)
                })
                .ToList();
        }
    }
}
