using BuergerPortal.Application.Appointments.DTOs;
using BuergerPortal.Application.Common;
using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Appointments.Entity;
using BuergerPortal.Domain.Appointments.Enums;
using FluentValidation;
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
        private readonly IValidator<AppointmentCreateDto> _validator;
        private readonly TimeProvider _time;
        private readonly IAppointmentBookingLock _bookingLock;

        public AppointmentBusinessService(IAppointmentRepository repo, IValidator<AppointmentCreateDto> validator,
            TimeProvider time, IAppointmentBookingLock bookingLock)
        {
            _repo = repo;
            _validator = validator;
            _time = time;
            _bookingLock = bookingLock;
        }

        // Meldungen bei belegten Zeiten (die Oberfläche zeigt sie unverändert an)
        public const string LocationTakenMessage =
            "Dieser Zeitraum ist am gewählten Standort bereits vergeben. Bitte wählen Sie eine andere Uhrzeit oder einen anderen Standort.";
        public const string OwnOverlapMessage = "Sie haben zu dieser Zeit bereits einen anderen Termin.";
        public const string BusyMessage =
            "Die Terminvergabe ist gerade stark ausgelastet. Bitte versuchen Sie es in einem Moment erneut.";

        public async Task<Result<Guid>> BookAsync(AppointmentCreateDto dto, Guid currentUserId, CancellationToken ct)
        {
            if (currentUserId == Guid.Empty)
                return Result<Guid>.Fail(ErrorCodes.Validation, "Ungültige Benutzer-ID.");

            var vr = await _validator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
            {
                var msg = string.Join(" ", vr.Errors.Select(e => e.ErrorMessage).Distinct());
                return Result<Guid>.Fail(ErrorCodes.Validation, msg);
            }

            // Prüfen und Speichern unter einer Sperre je Person und Standort: Ohne sie sahen zwei gleichzeitige Anfragen
            // beide „frei“ und buchten denselben Zeitraum (IAppointmentBookingLock).
            IAppointmentBookingLease lease;
            try
            {
                lease = await _bookingLock.AcquireAsync(currentUserId, dto.Location, ct);
            }
            catch (TimeoutException)
            {
                return Result<Guid>.Fail(ErrorCodes.SlotConflict, BusyMessage);
            }

            await using (lease)
            {
                // Ein Bürgeramt hat einen Schalter: Der Zeitraum darf am Standort noch nicht vergeben sein (vorher wurde nur
                // gegen die eigenen Termine geprüft – zwei Personen konnten denselben Termin buchen).
                if (await _repo.ExistsLocationOverlapAsync(dto.Location, dto.StartUtc, dto.EndUtc, null, ct))
                    return Result<Guid>.Fail(ErrorCodes.SlotConflict, LocationTakenMessage);

                // Niemand kann zur selben Zeit an zwei Orten sein.
                if (await _repo.ExistsOverlapAsync(currentUserId, dto.StartUtc, dto.EndUtc, ct))
                    return Result<Guid>.Fail(ErrorCodes.SlotConflict, OwnOverlapMessage);

                var entity = new Appointment {
                    Id = Guid.NewGuid(),
                    Service = dto.Service,
                    Location = dto.Location,
                    StartUtc = dto.StartUtc,
                    EndUtc = dto.EndUtc,
                    UserId = currentUserId,
                    Status = AppointmentStatus.Booked,
                    AntragId=dto.AntragId
                };
                await _repo.CreateAsync(entity, ct);
                await lease.CommitAsync(ct);
                return Result<Guid>.Success(entity.Id);
            }
        }

        public async Task<List<AppointmentListItemDto>> GetAllForUserAsync(Guid userId, CancellationToken ct)
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
                    Cancelled = x.Status == AppointmentStatus.Cancelled,
                    AntragId = x.AntragId

                })
                .ToList();
        }
        public async Task<List<BusySlotDto>> GetBusyAsync(DateTime fromUtc, DateTime toUtc, LocationType? location,
            CancellationToken ct)
        {
            // Guard
            if (fromUtc.Kind != DateTimeKind.Utc) fromUtc = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
            if (toUtc.Kind != DateTimeKind.Utc) toUtc = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc);
            if (toUtc <= fromUtc) return new List<BusySlotDto>();

            var overlaps = await _repo.GetOverlappingAsync(fromUtc, toUtc, location, ct);
            return overlaps
                .Select(a => new BusySlotDto
                {
                    StartUtc = DateTime.SpecifyKind(a.StartUtc, DateTimeKind.Utc),
                    EndUtc = DateTime.SpecifyKind(a.EndUtc, DateTimeKind.Utc)
                })
                .ToList();
        }
        public async Task<Result<Guid>> CancelAsync(Guid id, Guid currentUserId, CancellationToken ct)
        {
            if (id == Guid.Empty || currentUserId==Guid.Empty)
                return Result<Guid>.Fail(ErrorCodes.Validation, "Ungültige Eingaben.");

            var appt = await _repo.GetByIdAsync(id, ct);
            if (appt is null)
                return Result<Guid>.Fail(ErrorCodes.NotFound, "Termin nicht gefunden.");

            if (appt.UserId != currentUserId) // oder Rollenprüfung
                return Result<Guid>.Fail(ErrorCodes.Forbidden, "Keine Berechtigung.");

            if (appt.Status == AppointmentStatus.Cancelled)
                return Result<Guid>.Fail(ErrorCodes.Validation, "Termin ist bereits storniert.");

            if (appt.StartUtc <= _time.GetUtcNow().UtcDateTime)
                return Result<Guid>.Fail(ErrorCodes.Validation, "Vergangene Termine können nicht storniert werden.");

            appt.Status = AppointmentStatus.Cancelled;
            

            await _repo.UpdateAsync(appt, ct);
            return Result<Guid>.Success(appt.Id);
        }

        public async Task<Result<Guid>> DeleteAsync(Guid id, Guid currentUserId, CancellationToken ct)
        {
            if (id == Guid.Empty || currentUserId == Guid.Empty)
                return Result<Guid>.Fail(ErrorCodes.Validation, "Ungültige Eingaben.");

            var appt = await _repo.GetByIdAsync(id, ct);
            if (appt is null)
                return Result<Guid>.Fail(ErrorCodes.NotFound, "Termin nicht gefunden.");

            if (appt.UserId != currentUserId)
                return Result<Guid>.Fail(ErrorCodes.Forbidden, "Keine Berechtigung.");

            await _repo.DeleteAsync(appt, ct);
            return Result<Guid>.Success(id);
        }

        public async Task<AppointmentListItemDto?> GetByIdAsync(Guid id, Guid currentUserId, CancellationToken ct)
        {
            var appt = await _repo.GetByIdAsync(id, ct);
            if (appt is null || appt.UserId != currentUserId) return null;

            return new AppointmentListItemDto
            {
                Id = appt.Id,
                Service = appt.Service,
                Location = appt.Location,
                StartUtc = appt.StartUtc,
                EndUtc = appt.EndUtc,
                Cancelled = appt.Status == AppointmentStatus.Cancelled,
                AntragId = appt.AntragId
            };
        }

        public async Task<Result<Guid>> UpdateLocationAsync(Guid id, Guid currentUserId, LocationType newLocation, CancellationToken ct)
        {
            var appt = await _repo.GetByIdAsync(id, ct);

            if (appt is null)
                return Result<Guid>.Fail(ErrorCodes.NotFound, "Termin nicht gefunden.");

            if (appt.UserId != currentUserId)
                return Result<Guid>.Fail(ErrorCodes.Forbidden, "Keine Berechtigung.");

            if (appt.Status == AppointmentStatus.Cancelled)
                return Result<Guid>.Fail(ErrorCodes.Validation, "Stornierte Termine können nicht geändert werden.");

            if (appt.StartUtc < _time.GetUtcNow().UtcDateTime)
                return Result<Guid>.Fail(ErrorCodes.Validation, "Vergangene Termine können nicht geändert werden.");

            if (appt.Location == newLocation)
                return Result<Guid>.Fail(ErrorCodes.Validation, "Der Termin findet bereits an diesem Standort statt.");

            // Wie beim Buchen: Prüfen und Speichern unter der Sperre für Person und neuen Standort.
            IAppointmentBookingLease lease;
            try
            {
                lease = await _bookingLock.AcquireAsync(currentUserId, newLocation, ct);
            }
            catch (TimeoutException)
            {
                return Result<Guid>.Fail(ErrorCodes.SlotConflict, BusyMessage);
            }

            await using (lease)
            {
                // Am neuen Standort muss die Zeit noch frei sein (der eigene Termin zählt nicht).
                if (await _repo.ExistsLocationOverlapAsync(newLocation, appt.StartUtc, appt.EndUtc, appt.Id, ct))
                    return Result<Guid>.Fail(ErrorCodes.SlotConflict,
                        "Am neuen Standort ist diese Zeit bereits vergeben. Bitte buchen Sie dort einen neuen Termin.");

                appt.Location = newLocation;
                await _repo.UpdateAsync(appt, ct);
                await lease.CommitAsync(ct);

                return Result<Guid>.Success(appt.Id);
            }
        }
    }
}
