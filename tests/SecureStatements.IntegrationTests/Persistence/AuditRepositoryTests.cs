using FluentAssertions;
using SecureStatements.Domain;
using SecureStatements.Infrastructure.Persistence;

namespace SecureStatements.IntegrationTests.Persistence;

[Collection(PostgresDbCollection.Name)]
public sealed class AuditRepositoryTests
{
    private readonly PostgresDbFixture _fixture;

    public AuditRepositoryTests(PostgresDbFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AddAsync_persistsEntry_withGeneratedIdentity()
    {
        var customer = $"cust-{Guid.NewGuid():N}";
        var entry = new AuditEntry
        {
            OccurredAt = DateTimeOffset.UtcNow,
            Action = AuditActions.Downloaded,
            CustomerId = customer,
            StatementId = Guid.NewGuid(),
            ClientIp = "10.0.0.1",
            Detail = null
        };

        await using (var ctx = _fixture.CreateContext())
        {
            await new AuditRepository(ctx).AddAsync(entry, CancellationToken.None);
        }

        await using (var ctx = _fixture.CreateContext())
        {
            var saved = ctx.AuditEntries.Where(a => a.CustomerId == customer).ToList();
            saved.Should().ContainSingle();
            saved[0].Id.Should().BeGreaterThan(0, "the store assigns an identity key");
            saved[0].Action.Should().Be(AuditActions.Downloaded);
        }
    }
}


