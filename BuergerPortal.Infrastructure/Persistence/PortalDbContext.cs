using BuergerPortal.Domain.Appointments.Entity;
using BuergerPortal.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Infrastructure.Persistence
{
    public sealed class PortalDbContext : DbContext
    {
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public PortalDbContext(DbContextOptions<PortalDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder b)
        {
            var e = b.Entity<Appointment>();

            e.ToTable("Appointments");
            e.HasKey(x => x.Id);

            // Concurrency-Token (alternativ hast du [Timestamp] am Property)
            e.Property(x => x.RowVersion).IsRowVersion();

            // Pflichtfelder
            e.Property(x => x.StartUtc).IsRequired();
            e.Property(x => x.EndUtc).IsRequired();
            e.Property(x => x.UserId).IsRequired();
        }
    }
}
