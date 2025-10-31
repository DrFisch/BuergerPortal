using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Appointments.Entity;
using BuergerPortal.Domain.Appointments.Enums;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Infrastructure.Repositories
{
    public sealed class AppointmentRepository : IAppointmentRepository
    {
        private readonly PortalDbContext _db;
        public AppointmentRepository(PortalDbContext db) => _db = db;

        public Task<bool> ExistsOverlapAsync(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken ct)
            => _db.Appointments.AnyAsync(a =>
                   a.UserId == userId &&
                   a.Status == AppointmentStatus.Booked &&
                   !(a.EndUtc <= startUtc || a.StartUtc >= endUtc),
               ct);

        public async Task CreateAsync(Appointment entity, CancellationToken ct)
        {
            _db.Appointments.Add(entity);
            await _db.SaveChangesAsync(ct);
        }
        public Task<List<Appointment>> GetAllForUserAsync(Guid userId, CancellationToken ct)
        {
            return _db.Appointments
              .AsNoTracking()
              .Where(a => a.UserId == userId)
              .OrderBy(a => a.StartUtc)
              .ToListAsync(ct);
        }
        public Task<List<Appointment>> GetOverlappingAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return _db.Appointments
          .AsNoTracking()
          .Where(a => a.Status == AppointmentStatus.Booked &&
                      a.StartUtc < toUtc && fromUtc < a.EndUtc)
          .ToListAsync(ct);
        }
        public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Appointments.AsTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

        public async Task UpdateAsync(Appointment entity, CancellationToken ct)
        {
            _db.Appointments.Update(entity);
            await _db.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Appointment entity, CancellationToken ct)
        {
            _db.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }
}
