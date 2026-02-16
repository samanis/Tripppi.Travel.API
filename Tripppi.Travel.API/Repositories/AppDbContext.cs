using Microsoft.EntityFrameworkCore;
using System;
using Tripppi.Travel.API.Domain;

namespace Tripppi.Travel.API.Infrastructure
{
    /// <summary>
    /// Minimal EF Core DbContext for read-only airport queries.
    /// Configures Airport as keyless entity with composite shadow key for InMemory testing.
    /// </summary>
    public sealed class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // DbSet for query-only access to the airports table.
        public DbSet<Airport> Airports { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Map to the actual Postgres table name (lower-case) and configure Airport as keyless.
            modelBuilder.Entity<Airport>(b =>
            {
                b.ToTable("airports");
                b.HasNoKey();
            });

            // For InMemory testing, add a shadow property as key
            var provider = Database.ProviderName ?? string.Empty;
            if (provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                modelBuilder.Entity<Airport>()
                    .Property<int>("Id")
                    .ValueGeneratedOnAdd();
                modelBuilder.Entity<Airport>()
                    .HasKey("Id");
            }
        }
    }
}