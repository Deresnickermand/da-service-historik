using DA.ServiceHistorik.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DA.ServiceHistorik.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SentReminder> SentReminders => Set<SentReminder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SentReminder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.LicensePlate, e.ReminderType, e.ServiceDate })
                  .IsUnique();
            entity.Property(e => e.LicensePlate).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ReminderType).HasConversion<string>();
        });
    }
}
