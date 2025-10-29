using BuergerPortal.Application.Appointments.DTOs;
using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Appointments;
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

        public async Task<Guid> BookAsync(AppointmentCreateDto dto, string currentUserId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
            { 
                throw new UnauthorizedAccessException("Kein Benutzerkontext."); 
            }
            if (string.IsNullOrWhiteSpace(dto.Location))
            { 
                throw new ArgumentException("Location ist erforderlich."); 
            }
            if (dto.StartUtc >= dto.EndUtc)
            { 
                throw new ArgumentException("Start muss vor End liegen."); 
            }
            if (dto.StartUtc < DateTime.UtcNow.AddMinutes(-1))
            { 
                throw new ArgumentException("Datum liegt in der Vergangenheit."); 
            }

            // einfache Kollisionprüfung
            var collides = await _repo.ExistsOverlapAsync(currentUserId, dto.StartUtc, dto.EndUtc, ct);
            if (collides) 
            { 
                throw new InvalidOperationException("Zeitfenster kollidiert mit einem bestehenden Termin."); 
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
            return entity.Id;
        }
    }
}
