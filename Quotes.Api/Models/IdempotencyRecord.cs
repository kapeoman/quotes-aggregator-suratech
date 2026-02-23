namespace Quotes.Api.Models
{
    public class IdempotencyRecord
    {
        public Guid Id { get; set; }
        public string Key { get; set; } = null!;
        public string RequestHash { get; set; } = null!;
        public int StatusCode { get; set; }
        public string ResponseBody { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
