namespace Quotes.Api.Infrastructure.Idempotency;

public interface IIdempotencyService
{
    Task<IdempotencyDecision> CheckAsync(string key, string requestHash, CancellationToken ct);
    Task StoreAsync(string key, string requestHash, int statusCode, string responseBody, CancellationToken ct);
}

public record IdempotencyDecision(bool Exists, bool HashMatches, int? StatusCode, string? ResponseBody);