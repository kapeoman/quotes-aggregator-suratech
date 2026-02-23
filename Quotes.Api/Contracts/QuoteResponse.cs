namespace Quotes.Api.Contracts;

public class QuoteResponse
{
    public Guid Id { get; set; }
    public string DocumentId { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}