using System;
using Microsoft.EntityFrameworkCore;
using PropertyRentalPortal.Web.Data;
using PropertyRentalPortal.Web.Data.Entities;
using System.Security.Cryptography;

namespace PropertyRentalPortal.Tests;



public sealed class SqliteTestApplicationDbContext : ApplicationDbContext
{
    public SqliteTestApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RentalApplication>()
            .Property(a => a.RowVersion)
            .ValueGeneratedNever()
            .IsConcurrencyToken();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RotateRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        RotateRowVersions();
        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RotateRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries<RentalApplication>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property(a => a.RowVersion).CurrentValue =
                    RandomNumberGenerator.GetBytes(8);
            }
        }
    }
}
