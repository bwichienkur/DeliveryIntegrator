using LeadDelivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeadDelivery.Infrastructure.Persistence;

public class LeadDeliveryDbContext : DbContext
{
    public LeadDeliveryDbContext(DbContextOptions<LeadDeliveryDbContext> options) : base(options) { }

    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<FieldCatalogEntry> FieldCatalogEntries => Set<FieldCatalogEntry>();
    public DbSet<FieldCatalogInference> FieldCatalogInferences => Set<FieldCatalogInference>();
    public DbSet<DeliveryConfiguration> DeliveryConfigurations => Set<DeliveryConfiguration>();
    public DbSet<DeliveryEndpoint> DeliveryEndpoints => Set<DeliveryEndpoint>();
    public DbSet<CredentialProfile> CredentialProfiles => Set<CredentialProfile>();
    public DbSet<DeliveryRun> DeliveryRuns => Set<DeliveryRun>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AllowedStoredProcedure> AllowedStoredProcedures => Set<AllowedStoredProcedure>();
    public DbSet<EnrichmentStepDefinition> EnrichmentSteps => Set<EnrichmentStepDefinition>();
    public DbSet<ValueLookupTable> ValueLookupTables => Set<ValueLookupTable>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Institution>().HasIndex(i => i.Code).IsUnique();

        modelBuilder.Entity<Lead>(b =>
        {
            b.HasIndex(l => l.InstitutionId);
            b.Property(l => l.LeadDocumentJson).HasColumnType("nvarchar(max)");
            b.Property(l => l.RawPayload).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<FieldCatalogEntry>(b =>
        {
            b.HasIndex(e => new { e.InstitutionId, e.Key }).IsUnique();
        });

        modelBuilder.Entity<IdempotencyRecord>(b =>
        {
            b.HasKey(i => i.Key);
            b.Property(i => i.Key).HasMaxLength(256);
        });

        modelBuilder.Entity<DeliveryConfiguration>(b =>
        {
            b.HasMany(c => c.Endpoints).WithOne(e => e.DeliveryConfiguration).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeliveryRun>(b =>
        {
            b.HasMany(r => r.Attempts).WithOne(a => a.DeliveryRun).OnDelete(DeleteBehavior.Cascade);
            b.Property(r => r.EnrichedLeadSnapshotJson).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<OutboxMessage>(b => b.HasIndex(o => new { o.Status, o.CreatedAt }));
    }
}