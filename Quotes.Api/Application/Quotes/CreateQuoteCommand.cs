namespace Quotes.Api.Application.Quotes;

public record CreateQuoteCommand(
    string IdempotencyKey,
    string DocumentId,
    decimal Amount,
    string Currency
);