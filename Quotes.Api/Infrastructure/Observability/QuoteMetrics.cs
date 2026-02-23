using Prometheus;

namespace Quotes.Api.Infrastructure.Observability;

public static class QuoteMetrics
{
    public static readonly Counter QuotesCreatedTotal =
        Metrics.CreateCounter(
            "quotes_created_total",
            "Total number of quotes successfully created."
        );

    public static readonly Counter IdempotencyReplaysTotal =
        Metrics.CreateCounter(
            "idempotency_replays_total",
            "Total number of idempotent replays."
        );
}