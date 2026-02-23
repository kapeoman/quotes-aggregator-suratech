namespace Quotes.Api.Models
{
    public class Quote
    {
        public Guid Id { get; set; }
        public string DocumentId { get; set; } = null!;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = null!;
        public string Status { get; set; } = "ISSUED";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
