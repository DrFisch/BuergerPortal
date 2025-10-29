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
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new AppointmentConfiguration());
        }
    }
}
