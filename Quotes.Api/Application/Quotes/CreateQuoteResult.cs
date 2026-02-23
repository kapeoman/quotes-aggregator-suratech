using Quotes.Api.Contracts;

namespace Quotes.Api.Application.Quotes;

public record CreateQuoteResult(
    int StatusCode,
    QuoteResponse Body,
    bool IsReplay
);