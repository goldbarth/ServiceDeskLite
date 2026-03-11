using FluentAssertions;

using ServiceDeskLite.Domain.Outbox;
using ServiceDeskLite.Infrastructure.InMemory.Persistence;

namespace ServiceDeskLite.Tests.Infrastructure.InMemory;

public class InMemoryOutboxRepositoryTests
{
    private readonly InMemoryStore _store = new();

    private (InMemoryOutboxRepository repo, InMemoryUnitOfWork uow) CreateSut()
    {
        var uow = new InMemoryUnitOfWork(_store);
        var repo = new InMemoryOutboxRepository(uow);
        return (repo, uow);
    }

    private static OutboxMessage CreateMessage(string eventType = "ticket.created")
        => new(OutboxMessageId.New(), eventType, """{"ticketId":"abc"}""", DateTimeOffset.UtcNow);

    [Fact]
    public async Task Message_is_not_visible_before_SaveChangesAsync()
    {
        var (repo, _) = CreateSut();

        await repo.AddAsync(CreateMessage());

        // Without SaveChangesAsync the store must remain empty.
        _store.SnapshotOutboxMessages().Should().BeEmpty();
    }

    [Fact]
    public async Task Message_is_visible_after_SaveChangesAsync()
    {
        var (repo, uow) = CreateSut();
        var message = CreateMessage();

        await repo.AddAsync(message);
        await uow.SaveChangesAsync();

        var snapshot = _store.SnapshotOutboxMessages();
        snapshot.Should().ContainSingle()
            .Which.Id.Should().Be(message.Id);
    }

    [Fact]
    public async Task Multiple_messages_are_all_persisted_atomically()
    {
        var (repo, uow) = CreateSut();
        var m1 = CreateMessage("ticket.created");
        var m2 = CreateMessage("ticket.created");

        await repo.AddAsync(m1);
        await repo.AddAsync(m2);
        await uow.SaveChangesAsync();

        _store.SnapshotOutboxMessages().Should().HaveCount(2);
    }

    [Fact]
    public async Task Pending_message_has_status_Pending()
    {
        var (repo, uow) = CreateSut();

        await repo.AddAsync(CreateMessage());
        await uow.SaveChangesAsync();

        _store.SnapshotOutboxMessages().Single().Status
            .Should().Be(OutboxMessageStatus.Pending);
    }

    [Fact]
    public void MarkDispatched_transitions_status_to_Dispatched()
    {
        var message = CreateMessage();
        var dispatchedAt = DateTimeOffset.UtcNow.AddSeconds(5);

        message.MarkDispatched(dispatchedAt);

        message.Status.Should().Be(OutboxMessageStatus.Dispatched);
        message.DispatchedAt.Should().Be(dispatchedAt);
    }
}