using Microsoft.EntityFrameworkCore;
using Quotes.Api.Data;
using Quotes.Api.Models;

namespace Quotes.Api.Infrastructure.Idempotency;

public class IdempotencyService : IIdempotencyService
{
    private readonly QuotesDbContext _db;

    public IdempotencyService(QuotesDbContext db) => _db = db;

    public async Task<IdempotencyDecision> CheckAsync(string key, string requestHash, CancellationToken ct)
    {
        var rec = await _db.IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, ct);

        if (rec is null) return new IdempotencyDecision(false, false, null, null);

        var matches = string.Equals(rec.RequestHash, requestHash, StringComparison.OrdinalIgnoreCase);
        return new IdempotencyDecision(true, matches, rec.StatusCode, rec.ResponseBody);
    }

    public async Task StoreAsync(string key, string requestHash, int statusCode, string responseBody, CancellationToken ct)
    {
        _db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Key = key,
            RequestHash = requestHash,
            StatusCode = statusCode,
            ResponseBody = responseBody,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}