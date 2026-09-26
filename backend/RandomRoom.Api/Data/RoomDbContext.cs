using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Domain;

namespace RandomRoom.Api.Data;

public sealed class RoomDbContext(DbContextOptions<RoomDbContext> options) : DbContext(options)
{
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<RandomEvent> RandomEvents => Set<RandomEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Round>(round =>
        {
            round.HasIndex(r => r.Number).IsUnique();
            round.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<RandomEvent>(evt =>
        {
            // Database-level guarantee of one trigger per player per round, even under races.
            evt.HasIndex(e => new { e.RoundId, e.TriggeredBy }).IsUnique();
            evt.HasIndex(e => e.Timestamp);
            evt.Property(e => e.TriggeredBy).HasMaxLength(32);
            evt.Property(e => e.Result).HasMaxLength(32);
            evt.HasOne(e => e.Round).WithMany().HasForeignKey(e => e.RoundId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectRandomEventMutations();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RejectRandomEventMutations();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RejectRandomEventMutations()
    {
        var mutated = ChangeTracker.Entries<RandomEvent>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (mutated)
        {
            throw new InvalidOperationException("Random events are immutable once recorded.");
        }
    }
}
