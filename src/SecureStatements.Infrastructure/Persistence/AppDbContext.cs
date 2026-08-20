using Microsoft.EntityFrameworkCore;
using SecureStatements.Domain;

namespace SecureStatements.Infrastructure.Persistence;

// The EF Core context. Maps our two entities to tables and configures lengths and the indexes that make "list my statements" and audit lookups fast.
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Statement> Statements => Set<Statement>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var statement = modelBuilder.Entity<Statement>();
        statement.HasKey(s => s.Id);
        statement.Property(s => s.CustomerId).HasMaxLength(128).IsRequired();
        statement.Property(s => s.Period).HasMaxLength(32).IsRequired();
        statement.Property(s => s.FileName).HasMaxLength(256).IsRequired();
        statement.Property(s => s.BlobKey).HasMaxLength(256).IsRequired();
        statement.HasIndex(s => new { s.CustomerId, s.CreatedAt });

        var audit = modelBuilder.Entity<AuditEntry>();
        audit.HasKey(a => a.Id);
        audit.Property(a => a.Action).HasMaxLength(64).IsRequired();
        audit.Property(a => a.CustomerId).HasMaxLength(128).IsRequired();
        audit.Property(a => a.ClientIp).HasMaxLength(64);
        audit.Property(a => a.Detail).HasMaxLength(512);
        audit.HasIndex(a => new { a.CustomerId, a.OccurredAt });
    }
}