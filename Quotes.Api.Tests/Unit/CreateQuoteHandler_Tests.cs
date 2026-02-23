using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Quotes.Api.Application.Quotes;
using Quotes.Api.Contracts;
using Quotes.Api.Data;
using Quotes.Api.Infrastructure.Idempotency;
using Xunit;

namespace Quotes.Api.Tests.Unit;

public class CreateQuoteHandler_Tests
{
    [Fact]
    public async Task HandleAsync_FirstCall_CreatesQuoteAndStoresIdempotency()
    {
        await using var db = NewDb();
        var idem = new IdempotencyService(db);
        var handler = new CreateQuoteHandler(db, idem);

        var request = new CreateQuoteRequest { DocumentId = "DOC-U1", Amount = 99.9m, Currency = "clp" };
        var cmd = new CreateQuoteCommand("key-u1", request.DocumentId, request.Amount, request.Currency);

        var (result, errorCode, _) = await handler.HandleAsync(cmd, request, CancellationToken.None);

        errorCode.Should().BeNull();
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(201);
        result.IsReplay.Should().BeFalse();

        (await db.Quotes.CountAsync()).Should().Be(1);
        (await db.IdempotencyRecords.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_SameKeySamePayload_ReplaysStoredResponse()
    {
        await using var db = NewDb();
        var idem = new IdempotencyService(db);
        var handler = new CreateQuoteHandler(db, idem);

        var request = new CreateQuoteRequest { DocumentId = "DOC-U2", Amount = 10m, Currency = "usd" };
        var cmd = new CreateQuoteCommand("key-u2", request.DocumentId, request.Amount, request.Currency);

        var first = await handler.HandleAsync(cmd, request, CancellationToken.None);
        var second = await handler.HandleAsync(cmd, request, CancellationToken.None);

        first.errorCode.Should().BeNull();
        second.errorCode.Should().BeNull();
        second.result!.IsReplay.Should().BeTrue();
        second.result.Body.Id.Should().Be(first.result!.Body.Id);

        (await db.Quotes.CountAsync()).Should().Be(1);
        (await db.IdempotencyRecords.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_SameKeyDifferentPayload_ReturnsConflict()
    {
        await using var db = NewDb();
        var idem = new IdempotencyService(db);
        var handler = new CreateQuoteHandler(db, idem);

        var req1 = new CreateQuoteRequest { DocumentId = "DOC-U3", Amount = 10m, Currency = "clp" };
        var req2 = new CreateQuoteRequest { DocumentId = "DOC-U3", Amount = 11m, Currency = "clp" };

        var cmd1 = new CreateQuoteCommand("key-u3", req1.DocumentId, req1.Amount, req1.Currency);
        var cmd2 = new CreateQuoteCommand("key-u3", req2.DocumentId, req2.Amount, req2.Currency);

        var first = await handler.HandleAsync(cmd1, req1, CancellationToken.None);
        var second = await handler.HandleAsync(cmd2, req2, CancellationToken.None);

        first.errorCode.Should().BeNull();
        second.errorCode.Should().Be("IDEMPOTENCY_KEY_REUSE_CONFLICT");

        (await db.Quotes.CountAsync()).Should().Be(1);
        (await db.IdempotencyRecords.CountAsync()).Should().Be(1);
    }

    private static QuotesDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<QuotesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new QuotesDbContext(opts);
    }
}
