using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Common;
using InternshipCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Tests;

public class UserOwnedFilterTests
{
    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();

    [Fact]
    public async Task User_cannot_read_rows_owned_by_another_user()
    {
        var options = NewOptions();
        await Seed(options, new Note(UserA, "a"), new Note(UserB, "b"));

        await using var asA = new TestDbContext(options, new FakeCurrentUser(UserA));
        var notes = await asA.Notes.ToListAsync();

        var note = Assert.Single(notes);
        Assert.Equal("a", note.Text);
    }

    [Fact]
    public async Task User_cannot_save_a_row_for_another_user()
    {
        var options = NewOptions();

        await using var asA = new TestDbContext(options, new FakeCurrentUser(UserA));
        asA.Notes.Add(new Note(UserB, "sneaky"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => asA.SaveChangesAsync());
    }

    [Fact]
    public async Task User_can_save_own_rows()
    {
        var options = NewOptions();

        await using var asA = new TestDbContext(options, new FakeCurrentUser(UserA));
        asA.Notes.Add(new Note(UserA, "mine"));
        await asA.SaveChangesAsync();

        Assert.Single(await asA.Notes.ToListAsync());
    }

    [Fact]
    public async Task Without_a_current_user_nothing_is_visible_and_nothing_is_saved()
    {
        var options = NewOptions();
        await Seed(options, new Note(UserA, "a"));

        await using var anonymous = new TestDbContext(options, null);
        Assert.Empty(await anonymous.Notes.ToListAsync());

        anonymous.Notes.Add(new Note(UserA, "x"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => anonymous.SaveChangesAsync());
    }

    private static DbContextOptions NewOptions() =>
        new DbContextOptionsBuilder().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private static async Task Seed(DbContextOptions options, params Note[] notes)
    {
        foreach (var group in notes.GroupBy(note => note.UserId))
        {
            await using var context = new TestDbContext(options, new FakeCurrentUser(group.Key));
            context.Notes.AddRange(group);
            await context.SaveChangesAsync();
        }
    }

    private sealed class Note(Guid userId, string text) : IUserOwned
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid UserId { get; private set; } = userId;
        public string Text { get; private set; } = text;
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class TestDbContext(DbContextOptions options, ICurrentUser? currentUser)
        : InternshipCoachDbContext(options, currentUser)
    {
        public DbSet<Note> Notes => Set<Note>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Note>().HasKey(note => note.Id);
            base.OnModelCreating(modelBuilder);
        }
    }
}
