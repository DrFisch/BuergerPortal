using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;


namespace AuthenticationServer.Data
{
    public class ApplicationUser : IdentityUser
    {
        // --- BundID: nur was zur Wiedererkennung und für den Postkorb nötig ist (Datensparsamkeit) ---

        // Bereichsspezifisches Personenkennzeichen der BundID: dauerhafte, eindeutige Kennung der Person.
        [MaxLength(256)]
        public string? Bpk2 { get; set; }

        // Postkorb-Handle (UUID) für Nachrichten an das BundID-Postfach.
        [MaxLength(64)]
        public string? PostkorbHandle { get; set; }

        // Beim letzten Login erreichtes Vertrauensniveau (STORK-QAA-Level 1, 3 oder 4).
        public int? TrustLevel { get; set; }

        public DateTime? LastLoginUtc { get; set; }

        // Zeitpunkt, zu dem das Konto beim ersten BundID-Login angelegt wurde.
        public DateTime? CreatedViaBundIdUtc { get; set; }
    }
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<DataProtectionKey>().ToTable("DataProtectionKeys");
            builder.UseOpenIddict();

            // Eine bPK2 gehört zu genau einem Konto (alte lokale Konten haben keine bPK2).
            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.Bpk2)
                .IsUnique()
                .HasFilter("[Bpk2] IS NOT NULL");
        }
    }
}
