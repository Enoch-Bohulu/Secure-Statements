using FluentAssertions;
using SecureStatements.Domain;
using SecureStatements.Infrastructure.Persistence;

namespace SecureStatements.IntegrationTests.Persistence;

[Collection(PostgresDbCollection.Name)]
public sealed class StatementRepositoryTests
{
    private readonly PostgresDbFixture _fixture;

    public StatementRepositoryTests(PostgresDbFixture fixture) => _fixture = fixture;

    private static Statement New(string customerId, DateTimeOffset createdAt, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        CustomerId = customerId,
        Period = "2026-07",
        FileName = "s.pdf",
        BlobKey = Guid.NewGuid().ToString("N"),
        SizeBytes = 10,
        CreatedAt = createdAt
    };

    [Fact]
    public async Task AddAsync_thenList_persistsRow()
    {
        var customer = $"cust-{Guid.NewGuid():N}";
        await using (var ctx = _fixture.CreateContext())
        {
            var repo = new StatementRepository(ctx);
            await repo.AddAsync(New(customer, DateTimeOffset.UtcNow), CancellationToken.None);
        }

        await using (var ctx = _fixture.CreateContext())
        {
            var repo = new StatementRepository(ctx);
            var rows = await repo.ListByCustomerAsync(customer, CancellationToken.None);
            rows.Should().HaveCount(1);
        }
    }

    [Fact]
    public async Task ListByCustomerAsync_returnsOnlyThatCustomer_newestFirst()
    {
        var mine = $"mine-{Guid.NewGuid():N}";
        var other = $"other-{Guid.NewGuid():N}";

        await using (var ctx = _fixture.CreateContext())
        {
            var repo = new StatementRepository(ctx);
            await repo.AddAsync(New(mine, DateTimeOffset.UtcNow.AddDays(-2)), CancellationToken.None);
            await repo.AddAsync(New(mine, DateTimeOffset.UtcNow), CancellationToken.None);           // newest
            await repo.AddAsync(New(mine, DateTimeOffset.UtcNow.AddDays(-1)), CancellationToken.None);
            await repo.AddAsync(New(other, DateTimeOffset.UtcNow), CancellationToken.None);
        }

        await using (var ctx = _fixture.CreateContext())
        {
            var repo = new StatementRepository(ctx);
            var rows = await repo.ListByCustomerAsync(mine, CancellationToken.None);

            rows.Should().HaveCount(3);
            rows.Should().OnlyContain(s => s.CustomerId == mine);
            rows.Should().BeInDescendingOrder(s => s.CreatedAt);
        }
    }

    [Fact]
    public async Task GetOwnedAsync_wrongCustomer_returnsNull()
    {
        var owner = $"owner-{Guid.NewGuid():N}";
        var statement = New(owner, DateTimeOffset.UtcNow);

        await using (var ctx = _fixture.CreateContext())
        {
            await new StatementRepository(ctx).AddAsync(statement, CancellationToken.None);
        }

        await using (var ctx = _fixture.CreateContext())
        {
            var repo = new StatementRepository(ctx);
            var found = await repo.GetOwnedAsync(statement.Id, "someone-else", CancellationToken.None);
            found.Should().BeNull("ownership is part of the query predicate");
        }
    }

    [Fact]
    public async Task GetOwnedAsync_correctCustomer_returnsRow()
    {
        var owner = $"owner-{Guid.NewGuid():N}";
        var statement = New(owner, DateTimeOffset.UtcNow);

        await using (var ctx = _fixture.CreateContext())
        {
            await new StatementRepository(ctx).AddAsync(statement, CancellationToken.None);
        }

        await using (var ctx = _fixture.CreateContext())
        {
            var repo = new StatementRepository(ctx);
            var found = await repo.GetOwnedAsync(statement.Id, owner, CancellationToken.None);
            found.Should().NotBeNull();
            found!.Id.Should().Be(statement.Id);
        }
    }
}


