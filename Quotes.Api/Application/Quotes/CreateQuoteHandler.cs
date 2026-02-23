using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Quotes.Api.Contracts;
using Quotes.Api.Data;
using Quotes.Api.Infrastructure.Idempotency;
using Quotes.Api.Infrastructure.Observability;
using Quotes.Api.Models;

namespace Quotes.Api.Application.Quotes;

public class CreateQuoteHandler
{
    private readonly QuotesDbContext _db;
    private readonly IIdempotencyService _idempotency;

    public CreateQuoteHandler(QuotesDbContext db, IIdempotencyService idempotency)
    {
        _db = db;
        _idempotency = idempotency;
    }

    public async Task<(CreateQuoteResult? result, string? errorCode, string? errorMessage)> HandleAsync(
        CreateQuoteCommand cmd,
        CreateQuoteRequest request,
        CancellationToken ct)
    {
        // Hash request
        var requestHash = Sha256Hex(JsonSerializer.Serialize(request));

        var decision = await _idempotency.CheckAsync(cmd.IdempotencyKey, requestHash, ct);

        if (decision.Exists && !decision.HashMatches)
            return (null, "IDEMPOTENCY_KEY_REUSE_CONFLICT", "Idempotency-Key was already used with a different request payload.");

        if (decision.Exists && decision.HashMatches && decision.ResponseBody is not null && decision.StatusCode is not null)
        {
            QuoteMetrics.IdempotencyReplaysTotal.Inc();
            var body = JsonSerializer.Deserialize<QuoteResponse>(decision.ResponseBody)!;
            return (new CreateQuoteResult(decision.StatusCode.Value, body, true), null, null);
        }

        // Create Quote
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            DocumentId = request.DocumentId.Trim(),
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Status = "ISSUED",
            CreatedAt = DateTime.UtcNow
        };

        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync(ct);
        QuoteMetrics.QuotesCreatedTotal.Inc();

        var response = new QuoteResponse
        {
            Id = quote.Id,
            DocumentId = quote.DocumentId,
            Amount = quote.Amount,
            Currency = quote.Currency,
            Status = quote.Status,
            CreatedAt = quote.CreatedAt
        };

        var responseJson = JsonSerializer.Serialize(response);
        await _idempotency.StoreAsync(cmd.IdempotencyKey, requestHash, StatusCodes.Status201Created, responseJson, ct);

        return (new CreateQuoteResult(StatusCodes.Status201Created, response, false), null, null);
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}