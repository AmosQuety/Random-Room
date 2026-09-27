using Microsoft.EntityFrameworkCore;
using RandomRoom.Api.Domain;

namespace RandomRoom.Api.Data;

public sealed class RoomDbContext(DbContextOptions<RoomDbContext> options) : DbContext(options)
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomChoice> RoomChoices => Set<RoomChoice>();
    public DbSet<RoomPlayer> RoomPlayers => Set<RoomPlayer>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<RandomEvent> RandomEvents => Set<RandomEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Room>(room =>
        {
            room.HasIndex(r => r.Slug).IsUnique();
            room.Property(r => r.Slug).HasMaxLength(16);
            room.Property(r => r.Title).HasMaxLength(80);
            room.Property(r => r.HostPlayer).HasMaxLength(32);
        });

        modelBuilder.Entity<RoomChoice>(choice =>
        {
            choice.Property(c => c.Label).HasMaxLength(80);
            choice.HasOne<Room>().WithMany().HasForeignKey(c => c.RoomId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoomPlayer>(player =>
        {
            // A player name is only unique within its own room, not globally.
            player.HasIndex(p => new { p.RoomId, p.Name }).IsUnique();
            player.HasIndex(p => p.InviteToken).IsUnique().HasFilter("\"InviteToken\" IS NOT NULL");
            player.Property(p => p.Name).HasMaxLength(32);
            player.Property(p => p.PinHash).HasMaxLength(256);
            player.Property(p => p.InviteToken).HasMaxLength(64);
            player.HasOne(p => p.Room).WithMany().HasForeignKey(p => p.RoomId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Round>(round =>
        {
            round.HasIndex(r => new { r.RoomId, r.Number }).IsUnique();
            round.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            round.HasOne<Room>().WithMany().HasForeignKey(r => r.RoomId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RandomEvent>(evt =>
        {
            // Database-level guarantee of one trigger per player per round, even under races.
            evt.HasIndex(e => new { e.RoundId, e.TriggeredBy }).IsUnique();
            evt.HasIndex(e => e.Timestamp);
            evt.Property(e => e.TriggeredBy).HasMaxLength(32);
            evt.Property(e => e.Result).HasMaxLength(80);
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
