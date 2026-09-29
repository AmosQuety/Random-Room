using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RandomRoom.Api.Domain;

namespace RandomRoom.Api.Data;

public sealed class RoomDbContext(DbContextOptions<RoomDbContext> options) : DbContext(options)
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomChoice> RoomChoices => Set<RoomChoice>();
    public DbSet<RoomPlayer> RoomPlayers => Set<RoomPlayer>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<RandomPickerEvent> RandomPickerEvents => Set<RandomPickerEvent>();
    public DbSet<TriviaQuestion> TriviaQuestions => Set<TriviaQuestion>();
    public DbSet<TriviaSessionState> TriviaSessionStates => Set<TriviaSessionState>();
    public DbSet<TriviaAnswer> TriviaAnswers => Set<TriviaAnswer>();
    public DbSet<RoomGameSetup> RoomGameSetups => Set<RoomGameSetup>();
    public DbSet<GameSessionState> GameSessionStates => Set<GameSessionState>();
    public DbSet<GameEntry> GameEntries => Set<GameEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Room>(room =>
        {
            room.HasIndex(r => r.Slug).IsUnique();
            room.Property(r => r.Slug).HasMaxLength(16);
            room.Property(r => r.Title).HasMaxLength(80);
            room.Property(r => r.GameType).HasMaxLength(32);
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

        modelBuilder.Entity<GameSession>(session =>
        {
            session.HasIndex(s => new { s.RoomId, s.Number }).IsUnique();
            session.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            session.HasOne<Room>().WithMany().HasForeignKey(s => s.RoomId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RandomPickerEvent>(evt =>
        {
            // Database-level guarantee of one trigger per player per session, even under races.
            evt.HasIndex(e => new { e.SessionId, e.TriggeredBy }).IsUnique();
            evt.HasIndex(e => e.Timestamp);
            evt.Property(e => e.TriggeredBy).HasMaxLength(32);
            evt.Property(e => e.Result).HasMaxLength(80);
            evt.HasOne(e => e.Session).WithMany().HasForeignKey(e => e.SessionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TriviaQuestion>(q =>
        {
            q.Property(x => x.Text).HasMaxLength(300);
            q.Property(x => x.Options)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new())
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s)),
                    v => v.ToList()));
            q.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TriviaSessionState>(s =>
        {
            s.HasIndex(x => x.SessionId).IsUnique();
            s.HasOne<GameSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TriviaAnswer>(a =>
        {
            // Database-level guarantee of one answer per player per question per session, even under races.
            a.HasIndex(x => new { x.SessionId, x.QuestionId, x.TriggeredBy }).IsUnique();
            a.Property(x => x.TriggeredBy).HasMaxLength(32);
            a.HasOne<GameSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            a.HasOne<TriviaQuestion>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoomGameSetup>(setup =>
        {
            setup.HasIndex(x => x.RoomId).IsUnique();
            setup.Property(x => x.Json).HasColumnType("jsonb");
            setup.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameSessionState>(state =>
        {
            state.HasIndex(x => x.SessionId).IsUnique();
            state.Property(x => x.Phase).HasMaxLength(24);
            state.Property(x => x.Claimant).HasMaxLength(32);
            state.Property(x => x.DataJson).HasColumnType("jsonb");
            state.Property(x => x.Version).IsConcurrencyToken();
            state.HasOne<GameSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameEntry>(entry =>
        {
            // Database-level guarantee of one entry per player per kind per round (per seq), even under races.
            entry.HasIndex(x => new { x.SessionId, x.Round, x.Kind, x.Player, x.Seq }).IsUnique();
            entry.HasIndex(x => new { x.SessionId, x.Kind, x.Ordinal });
            entry.Property(x => x.Kind).HasMaxLength(24);
            entry.Property(x => x.Player).HasMaxLength(32);
            entry.Property(x => x.ValueJson).HasColumnType("jsonb");
            entry.Property(x => x.Ordinal).UseIdentityAlwaysColumn();
            entry.HasOne<GameSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectImmutableMutations();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RejectImmutableMutations();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RejectImmutableMutations()
    {
        var mutated = ChangeTracker.Entries<RandomPickerEvent>().Any(e => e.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<TriviaAnswer>().Any(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (mutated)
        {
            throw new InvalidOperationException("Recorded game events are immutable once recorded.");
        }
    }
}
