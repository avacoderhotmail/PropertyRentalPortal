using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PropertyRentalPortal.Web.Data.Entities;

namespace PropertyRentalPortal.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<UnitType> UnitTypes => Set<UnitType>();
    public DbSet<RentalApplication> RentalApplications => Set<RentalApplication>();
    public DbSet<ResidenceHistory> ResidenceHistories => Set<ResidenceHistory>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<PropertyManagerNote> PropertyManagerNotes => Set<PropertyManagerNote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Unit relationships
        builder.Entity<Unit>(entity =>
        {
            entity.HasOne(u => u.Property)
                .WithMany(p => p.Units)
                .HasForeignKey(u => u.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(u => u.UnitType)
                .WithMany(ut => ut.Units)
                .HasForeignKey(u => u.UnitTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(u => u.MonthlyRent)
                .HasPrecision(18, 2);

            entity.Property(u => u.Bathrooms)
                .HasPrecision(4, 1);
        });

        // Lease relationships
        builder.Entity<Lease>(entity =>
        {
            entity.HasOne(l => l.Unit)
                .WithMany(u => u.Leases)
                .HasForeignKey(l => l.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.RentalApplication)
                .WithOne(a => a.Lease)
                .HasForeignKey<Lease>(l => l.ApplicationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(l => l.Tenant)
                .WithMany(u => u.Leases)
                .HasForeignKey(l => l.TenantUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(l => l.MonthlyRent)
                .HasPrecision(18, 2);
        });

        // RentalApplication relationships
        builder.Entity<RentalApplication>(entity =>
        {
            entity.HasOne(a => a.Unit)
                .WithMany(u => u.Applications)
                .HasForeignKey(a => a.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.ApplicantUser)
                .WithMany(u => u.Applications)
                .HasForeignKey(a => a.ApplicantUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.ClaimedByUser)
                .WithMany()
                .HasForeignKey(a => a.ClaimedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(a => a.RowVersion)
                .IsRowVersion();
        });

        // ResidenceHistory relationships
        builder.Entity<ResidenceHistory>(entity =>
        {
            entity.HasOne(r => r.RentalApplication)
                .WithMany(a => a.Residences)
                .HasForeignKey(r => r.RentalApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ApplicationStatusHistory relationships
        builder.Entity<ApplicationStatusHistory>(entity =>
        {
            entity.HasOne(h => h.RentalApplication)
                .WithMany(a => a.StatusHistories)
                .HasForeignKey(h => h.RentalApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.ChangedByUser)
                .WithMany(u => u.StatusChangesMade)
                .HasForeignKey(h => h.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // PropertyManagerNote relationships
        builder.Entity<PropertyManagerNote>(entity =>
        {
            entity.HasOne(n => n.RentalApplication)
                .WithMany(a => a.Notes)
                .HasForeignKey(n => n.RentalApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(n => n.AuthorUser)
                .WithMany()
                .HasForeignKey(n => n.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
