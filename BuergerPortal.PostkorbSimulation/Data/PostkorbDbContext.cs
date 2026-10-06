using BuergerPortal.PostkorbSimulation.Models;
using Microsoft.EntityFrameworkCore;

namespace BuergerPortal.PostkorbSimulation.Data;

/// <summary>Datenbank des simulierten Postfachs (eigene Datenbank "PostkorbDB" auf derselben SQL-Server-Instanz).</summary>
public class PostkorbDbContext(DbContextOptions<PostkorbDbContext> options) : DbContext(options)
{
    public DbSet<PostkorbMessage> Messages => Set<PostkorbMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var message = modelBuilder.Entity<PostkorbMessage>();
        message.ToTable("Messages");
        message.HasKey(m => m.Id);
        message.Property(m => m.Title).HasMaxLength(PostkorbMessage.MaxTitleLength).IsRequired();
        message.Property(m => m.Content).IsRequired();
        message.Property(m => m.Sender).HasMaxLength(PostkorbMessage.MaxNameLength).IsRequired();
        message.Property(m => m.Service).HasMaxLength(PostkorbMessage.MaxNameLength).IsRequired();
        message.Property(m => m.ReplyAddress).HasMaxLength(PostkorbMessage.MaxAddressLength);

        // Postfachansicht: alle Nachrichten eines Postkorb-Handles, neueste zuerst.
        message.HasIndex(m => new { m.MailboxUuid, m.CreatedUtc });
    }
}
