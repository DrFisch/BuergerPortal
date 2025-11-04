using BuergerPortal.Domain.Antrag.Entity;
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
        public PortalDbContext(DbContextOptions<PortalDbContext> options) : base(options) { }

        // DbSets
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<Antrag> Antraege => Set<Antrag>();
        public DbSet<ReisepassAntrag> ReisepassAntraege => Set<ReisepassAntrag>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            // -------------------------
            // APPOINTMENT
            // -------------------------
            var appt = b.Entity<Appointment>();
            appt.ToTable("Appointments");
            appt.HasKey(x => x.Id);

            appt.Property(x => x.RowVersion).IsRowVersion();
            appt.Property(x => x.StartUtc).IsRequired();
            appt.Property(x => x.EndUtc).IsRequired();
            appt.Property(x => x.UserId).IsRequired();

            // sinnvolle Indizes für Suche/Verfügbarkeit
            appt.HasIndex(x => new { x.Service, x.StartUtc, x.EndUtc });
            appt.HasIndex(x => x.AntragId);

            // FK: Appointment (0..1) -> Antrag (1)
            // Löscht man den Antrag, bleibt der Termin erhalten (AntragId wird NULL)
            appt.HasOne<Antrag>()
                .WithMany()                         // (kein Navigations-Property nötig)
                .HasForeignKey(x => x.AntragId)
                .OnDelete(DeleteBehavior.SetNull);

            // Basis
            var baseAntrag = b.Entity<Antrag>();
            baseAntrag.ToTable("Antraege");
            baseAntrag.HasKey(x => x.Id);
            baseAntrag.Property(x => x.Typ).IsRequired();
            baseAntrag.Property(x => x.Status).IsRequired();
            baseAntrag.Property(x => x.ApplicantUserId).IsRequired(); // Achtung: bei dir Guid
            baseAntrag.Property(x => x.RowVersion).IsRowVersion();
            baseAntrag.HasIndex(x => new { x.ApplicantUserId, x.Typ, x.Status });

            // Abgeleitet (TPT): EF Core erstellt automatisch Shared-PK(FK) von ReisepassAntraege.Id -> Antraege.Id
            var pass = b.Entity<ReisepassAntrag>();
            pass.ToTable("ReisepassAntraege"); // <- das triggert TPT

            pass.OwnsOne(x => x.Name, n =>
            {
                n.Property(p => p.Vorname).HasMaxLength(100).HasColumnName("Vorname").IsRequired();
                n.Property(p => p.Nachname).HasMaxLength(100).HasColumnName("Nachname").IsRequired();
            });

            pass.OwnsOne(x => x.Birth, n =>
            {
                n.Property(p => p.Geburtsdatum).HasColumnName("Geburtsdatum").IsRequired();
            });

            pass.OwnsOne(x => x.Kontakt, n =>
            {
                n.Property(p => p.Email).HasMaxLength(200).HasColumnName("Email");
                n.Property(p => p.Telefon).HasMaxLength(50).HasColumnName("Telefon");
            });

            pass.Property(x => x.Hinweis).HasMaxLength(1000);
        }
    }
}
