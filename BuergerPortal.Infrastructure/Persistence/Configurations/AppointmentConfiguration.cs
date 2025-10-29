using BuergerPortal.Domain.Appointments;
using BuergerPortal.Domain.Appointments.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Infrastructure.Persistence.Configurations
{
    internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
    {
        public void Configure(EntityTypeBuilder<Appointment> b)
        {
            b.ToTable("Appointments");
            b.HasKey(x => x.Id);

            b.Property(x => x.Service)
             .HasConversion<string>()         // Enums als string speichern (lesbar, migrationsfreundlich)
             .IsRequired();

            b.Property(x => x.Location)
             .HasMaxLength(200)
             .HasDefaultValue("Bürgeramt Mitte")
             .IsRequired();

            b.Property(x => x.StartUtc).IsRequired();
            b.Property(x => x.EndUtc).IsRequired();

            b.Property(x => x.UserId)
             .HasMaxLength(100)
             .IsRequired();

            b.Property(x => x.Status)
             .HasConversion<string>()
             .HasDefaultValue(AppointmentStatus.Booked)
             .IsRequired();

            // Concurrency
            b.Property(x => x.RowVersion)
             .IsRowVersion()
             .IsConcurrencyToken();
        }
    }
}
