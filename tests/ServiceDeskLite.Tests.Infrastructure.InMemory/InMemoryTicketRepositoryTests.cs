using FluentAssertions;

using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Infrastructure.InMemory.Persistence;

namespace ServiceDeskLite.Tests.Infrastructure.InMemory;

public class InMemoryTicketRepositoryTests
{
    private readonly InMemoryStore _store = new();

    private (InMemoryTicketRepository repo, InMemoryUnitOfWork uow) CreateSut()
    {
        var uow = new InMemoryUnitOfWork(_store);
        var repo = new InMemoryTicketRepository(_store, uow);
        return (repo, uow);
    }

    private static Ticket CreateTicket(
        DateTimeOffset createdAt,
        string title = "Title",
        TicketId? id = null)
    {
        return new Ticket(
            id ?? TicketId.New(),
            title,
            "Description",
            TicketPriority.Medium,
            createdAt);
    }

    private async Task SeedCommitted(params Ticket[] tickets)
    {
        var (repo, uow) = CreateSut();
        foreach (var t in tickets)
            await repo.AddAsync(t);
        await uow.SaveChangesAsync();
    }

    // ── Search: Default Sort ────────────────────────────────────

    [Fact]
    public async Task SearchTickets_DefaultSort_IsCreatedAtDesc_ThenById()
    {
        var now = DateTimeOffset.UtcNow;

        // Two tickets with the same CreatedAt → tie-break by Id ascending
        var id1 = new TicketId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var id2 = new TicketId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var older = CreateTicket(now.AddHours(-1), "Older", id1);
        var newer = CreateTicket(now, "Newer-A", id2);
        var newerTie = CreateTicket(now, "Newer-B", new TicketId(Guid.Parse("00000000-0000-0000-0000-000000000003")));

        await SeedCommitted(older, newer, newerTie);

        var (repo, _) = CreateSut();
        var result = await repo.SearchAsync(
            new TicketSearchCriteria(),
            Paging.Default,
            SortSpec.Default);

        result.Items.Should().HaveCount(3);

        // Descending by CreatedAt, then ascending by Id for tie-break
        result.Items[0].Title.Should().Be("Newer-A");   // same time, lower Id
        result.Items[1].Title.Should().Be("Newer-B");   // same time, higher Id
        result.Items[2].Title.Should().Be("Older");      // earlier time
    }

    // ── Search: Paging ──────────────────────────────────────────

    [Fact]
    public async Task SearchTickets_Paging_Works()
    {
        var now = DateTimeOffset.UtcNow;
        var tickets = Enumerable.Range(1, 5)
            .Select(i => CreateTicket(now.AddMinutes(-i), $"Ticket-{i}"))
            .ToArray();

        await SeedCommitted(tickets);

        var (repo, _) = CreateSut();
        var page1 = await repo.SearchAsync(
            new TicketSearchCriteria(),
            new Paging(Page: 1, PageSize: 2),
            SortSpec.Default);

        page1.TotalCount.Should().Be(5);
        page1.Items.Should().HaveCount(2);

        var page3 = await repo.SearchAsync(
            new TicketSearchCriteria(),
            new Paging(Page: 3, PageSize: 2),
            SortSpec.Default);

        page3.TotalCount.Should().Be(5);
        page3.Items.Should().HaveCount(1);  // 5th item on last page
    }

    // ── UoW staging: not visible before commit ──────────────────

    [Fact]
    public async Task AddWithoutCommit_NotVisible()
    {
        var (repo, _) = CreateSut();
        var ticket = CreateTicket(DateTimeOffset.UtcNow);

        await repo.AddAsync(ticket);

        // Not committed → store is empty
        var found = await repo.GetByIdAsync(ticket.Id);
        found.Should().BeNull();
    }

    // ── UoW staging: visible after commit ───────────────────────

    [Fact]
    public async Task AddWithoutCommit_NotVisible_InNewUnitOfWork()
    {
        var ticket = CreateTicket(DateTimeOffset.UtcNow);

        var (repo, _) = CreateSut();
        await repo.AddAsync(ticket);

        var (readRepo, _) = CreateSut();
        var found = await readRepo.GetByIdAsync(ticket.Id);

        found.Should().BeNull();
    }
    
    [Fact]
    public async Task AddWithCommit_Visible_InNewUnitOfWork()
    {
        var ticket = CreateTicket(DateTimeOffset.UtcNow);

        var (repo, uow) = CreateSut();
        await repo.AddAsync(ticket);
        await uow.SaveChangesAsync();

        var (readRepo, _) = CreateSut();
        var found = await readRepo.GetByIdAsync(ticket.Id);

        found.Should().NotBeNull();
        found!.Id.Should().Be(ticket.Id);
    }

    [Fact]
    public async Task AddWithoutCommit_NotVisible_InSameUnitOfWork()
    {
        var ticket = CreateTicket(DateTimeOffset.UtcNow);

        var (repo, _) = CreateSut();
        await repo.AddAsync(ticket);

        var found = await repo.GetByIdAsync(ticket.Id);
        found.Should().BeNull();
    }


    // ── Duplicate detection on commit ───────────────────────────

    [Fact]
    public async Task DuplicateAdd_OnCommit_Throws()
    {
        var ticket = CreateTicket(DateTimeOffset.UtcNow);
        await SeedCommitted(ticket);

        // Second add with same Id → should throw on commit
        var (repo, uow) = CreateSut();
        await repo.AddAsync(ticket);

        var act = () => uow.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }
}
