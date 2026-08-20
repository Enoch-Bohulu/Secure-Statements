using System.Text;
using FluentAssertions;
using SecureStatements.Application.Services;
using SecureStatements.Domain;
using SecureStatements.UnitTests.Fakes;

namespace SecureStatements.UnitTests;

/// <summary>
/// Unit tests for <see cref="StatementService"/>, focusing on the central security rule —
/// a customer may only ever see their own statements — and correct ingestion behavior.
/// </summary>
public sealed class StatementServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 08, 17, 09, 30, 00, TimeSpan.Zero);

    private static Statement NewStatement(string customerId, DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
        Period = "2026-07",
        FileName = "statement.pdf",
        BlobKey = Guid.NewGuid().ToString("N"),
        SizeBytes = 1024,
        CreatedAt = createdAt
    };

    [Fact]
    public async Task GetOwnedAsync_returns_the_statement_when_it_belongs_to_the_caller()
    {
        var owned = NewStatement("C1", Now);
        var repository = new FakeStatementRepository().Seed(owned);
        var service = new StatementService(repository, new FakeBlobStore(), new FixedClock(Now));

        var result = await service.GetOwnedAsync(owned.Id, "C1", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(owned.Id);
    }

    [Fact]
    public async Task GetOwnedAsync_returns_null_when_another_customer_requests_the_statement()
    {
        // Insecure Direct Object Reference guard: C2 must not reach C1's statement.
        var owned = NewStatement("C1", Now);
        var repository = new FakeStatementRepository().Seed(owned);
        var service = new StatementService(repository, new FakeBlobStore(), new FixedClock(Now));

        var result = await service.GetOwnedAsync(owned.Id, "C2", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListForCustomerAsync_returns_only_the_requested_customers_statements()
    {
        var mine1 = NewStatement("C1", Now);
        var mine2 = NewStatement("C1", Now.AddMinutes(5));
        var theirs = NewStatement("C2", Now);
        var repository = new FakeStatementRepository().Seed(mine1).Seed(mine2).Seed(theirs);
        var service = new StatementService(repository, new FakeBlobStore(), new FixedClock(Now));

        var result = await service.ListForCustomerAsync("C1", CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(s => s.CustomerId == "C1");
    }

    [Fact]
    public async Task IngestAsync_saves_the_blob_then_persists_metadata_referencing_it()
    {
        var repository = new FakeStatementRepository();
        var blobStore = new FakeBlobStore();
        var clock = new FixedClock(Now);
        var service = new StatementService(repository, blobStore, clock);

        var content = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4 body"));

        var statement = await service.IngestAsync(
            customerId: "C1",
            period: "2026-07",
            fileName: "statement-C1-2026-07.pdf",
            content: content,
            sizeBytes: content.Length,
            cancellationToken: CancellationToken.None);

        // The blob was saved exactly once...
        blobStore.SavedKeys.Should().ContainSingle();

        // ...and the persisted metadata points at that exact blob key.
        repository.Added.Should().ContainSingle();
        var persisted = repository.Added.Single();
        persisted.BlobKey.Should().Be(blobStore.SavedKeys.Single());

        // The returned statement is the persisted one, with fields mapped and stamped.
        statement.Id.Should().NotBeEmpty();
        statement.CustomerId.Should().Be("C1");
        statement.Period.Should().Be("2026-07");
        statement.FileName.Should().Be("statement-C1-2026-07.pdf");
        statement.SizeBytes.Should().Be(content.Length);
        statement.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task OpenContentAsync_returns_the_bytes_that_were_ingested()
    {
        var repository = new FakeStatementRepository();
        var blobStore = new FakeBlobStore();
        var service = new StatementService(repository, blobStore, new FixedClock(Now));

        var originalBytes = Encoding.ASCII.GetBytes("%PDF-1.4 hello");
        var statement = await service.IngestAsync(
            "C1", "2026-07", "statement.pdf",
            new MemoryStream(originalBytes), originalBytes.Length, CancellationToken.None);

        await using var stream = await service.OpenContentAsync(statement, CancellationToken.None);
        stream.Should().NotBeNull();

        using var buffer = new MemoryStream();
        await stream!.CopyToAsync(buffer);
        buffer.ToArray().Should().Equal(originalBytes);
    }
}

