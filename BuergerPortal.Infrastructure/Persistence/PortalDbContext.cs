using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Domain.Appointments.Entity;
using BuergerPortal.Domain.Maengel;
using BuergerPortal.Domain.Poi.Entity;
using BuergerPortal.Domain.Settings.Entity;
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
        public DbSet<SperrmuellAntrag> SperrmuellAntraege => Set<SperrmuellAntrag>();
        public DbSet<Maengelmeldung> Maengelmeldungen => Set<Maengelmeldung>();
        public DbSet<UserSettings> UserSettings { get; set; } = default!;
        public DbSet<PoiEntity> Pois => Set<PoiEntity>();


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


            appt.HasOne<Antrag>()
                .WithMany()                        
                .HasForeignKey(x => x.AntragId)
                .OnDelete(DeleteBehavior.SetNull);

            // Basis
            var baseAntrag = b.Entity<Antrag>();
            baseAntrag.ToTable("Antraege");
            baseAntrag.HasKey(x => x.Id);
            baseAntrag.Property(x => x.Typ).IsRequired();
            baseAntrag.Property(x => x.Status).IsRequired();
            baseAntrag.Property(x => x.ApplicantUserId).IsRequired(); 
            baseAntrag.Property(x => x.RowVersion).IsRowVersion();
            baseAntrag.HasIndex(x => new { x.ApplicantUserId, x.Typ, x.Status });

            
            var pass = b.Entity<ReisepassAntrag>();
            pass.ToTable("ReisepassAntraege"); 

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

            // -------------------------------------------------
            // SPERRMÜLLANTRAG (TPT + Owned Types)
            // -------------------------------------------------
            var sperr = b.Entity<SperrmuellAntrag>();
            sperr.ToTable("SperrmuellAntraege"); 

            // PersonName
            sperr.OwnsOne(x => x.Name, n =>
            {
                n.Property(p => p.Vorname).HasMaxLength(100).HasColumnName("Vorname").IsRequired();
                n.Property(p => p.Nachname).HasMaxLength(100).HasColumnName("Nachname").IsRequired();
            });

            // PersonBirth
            sperr.OwnsOne(x => x.Birth, n =>
            {
                n.Property(p => p.Geburtsdatum).HasColumnName("Geburtsdatum").IsRequired();
            });

            // Kontakt
            sperr.OwnsOne(x => x.Kontakt, n =>
            {
                n.Property(p => p.Email).HasMaxLength(200).HasColumnName("Email");
                n.Property(p => p.Telefon).HasMaxLength(50).HasColumnName("Telefon");
            });

            // Addresse als Owned Type
            sperr.OwnsOne(x => x.Addresse, n =>
            {
                n.Property(p => p.Strasse)
                    .HasMaxLength(200)
                    .HasColumnName("Strasse"); 

                n.Property(p => p.PLZ)
                    .HasMaxLength(10)
                    .HasColumnName("PLZ");     

                n.Property(p => p.Ort)
                    .HasMaxLength(100)
                    .HasColumnName("Ort");     
            });


            // SperrmuellMengen als Owned Type
            sperr.OwnsOne(x => x.Mengen, n =>
            {
                n.Property(p => p.HolzKubikmeter)
                    .HasColumnName("HolzKubikmeter");

                n.Property(p => p.SonstigesKubikmeter)
                    .HasColumnName("SonstigesKubikmeter");

                n.Property(p => p.Matratzen)
                    .HasColumnName("Matratzen");
            });

            sperr.Property(x => x.Wunschzeit)
                 .IsRequired();

            sperr.Property(x => x.Hinweis)
                 .HasMaxLength(1000);

            // -------------------------
            // USER SETTINGS
            // -------------------------
            var settings = b.Entity<UserSettings>();
            settings.ToTable("UserSettings");
            settings.HasKey(x => x.Id);

            settings.Property(x => x.UserId).IsRequired();

           
            settings.HasIndex(x => x.UserId).IsUnique();

            
            settings.Property(x => x.Theme)
                    .HasConversion<string>()        // speichert "Light"/"Dark"
                    .IsRequired();

            settings.Property(x => x.Language)
                    .HasMaxLength(8)
                    .IsRequired();

            settings.Property(x => x.PushEnabled).IsRequired();
            settings.Property(x => x.ReduceDataUsage).IsRequired();
            settings.Property(x => x.AnalyticsOptIn).IsRequired();
            settings.Property(x => x.AllowGeolocation).IsRequired();

            settings.Property(x => x.RowVersion).IsRowVersion();

            settings.Property(x => x.UpdatedUtc)
                    .HasDefaultValueSql("GETUTCDATE()");

            // -------------------------
            // MAENGELMELDUNG
            // -------------------------
            var mangel = b.Entity<Maengelmeldung>();
            mangel.ToTable("Maengelmeldungen");
            mangel.HasKey(x => x.Id);

            mangel.Property(x => x.CreatedUtc).IsRequired();

            mangel.Property(x => x.ReporterUserId);

            mangel.Property(x => x.Titel)
                  .HasMaxLength(200)
                  .IsRequired();

            mangel.Property(x => x.Beschreibung)
                  .HasMaxLength(4000)
                  .IsRequired();

            mangel.Property(x => x.Latitude);
            mangel.Property(x => x.Longitude);

            mangel.Property(x => x.AddressHint)
                  .HasMaxLength(500);

            mangel.Property(x => x.Status)
                  .HasMaxLength(30)
                  .IsRequired();

            mangel.HasIndex(x => x.CreatedUtc);
            mangel.HasIndex(x => x.Status);

            // -------------------------
            // POI (INTERESSANTE ORTE)
            // -------------------------
            var poi = b.Entity<PoiEntity>();
            poi.ToTable("Pois");
            poi.HasKey(x => x.Id);

            poi.Property(x => x.Name).HasMaxLength(150).IsRequired();
            poi.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            poi.Property(x => x.Tags).HasMaxLength(500);
            poi.Property(x => x.Icon).HasMaxLength(50).IsRequired();
            poi.Property(x => x.Category).HasConversion<string>().IsRequired(); // Speichert Enum als String (Verwaltung, Kultur...)

            poi.Property(x => x.RowVersion).IsRowVersion();

            // Indizes für die Suche
            poi.HasIndex(x => x.Name);
            poi.HasIndex(x => x.Category);
        }
    }
}
