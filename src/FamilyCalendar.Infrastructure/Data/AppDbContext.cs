using FamilyCalendar.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyCalendar.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Email> Emails => Set<Email>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Email>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Classification).HasConversion<string>();
            e.HasMany(x => x.Events).WithOne(x => x.Email).HasForeignKey(x => x.EmailId);
            e.HasIndex(x => x.MessageId).IsUnique();
            e.HasIndex(x => x.Classification);
        });

        modelBuilder.Entity<CalendarEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.StartTime);
            e.HasIndex(x => x.FamilyMemberName);
        });

        modelBuilder.Entity<FamilyMember>(e =>
        {
            e.HasKey(x => x.Id);
            e.ToTable("FamilyMembers");
        });

        // Seed family members
        modelBuilder.Entity<FamilyMember>().HasData(
            new FamilyMember { Id = Guid.Parse("11111111-0000-0000-0000-000000000001"), Name = "Vera", Description = "Går i klass 5 på Vattholmaskolan." },
            new FamilyMember { Id = Guid.Parse("11111111-0000-0000-0000-000000000002"), Name = "Tage", Description = "Går i klass 3 på Vattholmaskolan." },
            new FamilyMember { Id = Guid.Parse("11111111-0000-0000-0000-000000000003"), Name = "Sixten", Description = "Går på Hyttans förskola." },
            new FamilyMember { Id = Guid.Parse("11111111-0000-0000-0000-000000000004"), Name = "Folke", Description = "Går på Hyttans förskola." },
            new FamilyMember { Id = Guid.Parse("11111111-0000-0000-0000-000000000005"), Name = "Micke" },
            new FamilyMember { Id = Guid.Parse("11111111-0000-0000-0000-000000000006"), Name = "Emelie" }
        );
    }
}
