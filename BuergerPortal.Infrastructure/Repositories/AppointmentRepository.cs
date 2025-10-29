using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Appointments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Infrastructure.Repositories
{
    public sealed class EfAppointmentRepository : IAppointmentRepository
    {
        private readonly AppDbContext _db;
        public EfAppointmentRepository(AppDbContext db) => _db = db;

        public Task<bool> ExistsOverlapAsync(string userId, DateTime startUtc, DateTime endUtc, CancellationToken ct)
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
    }
}
