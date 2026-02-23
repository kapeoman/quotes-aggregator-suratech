using System.ComponentModel.DataAnnotations;

namespace Quotes.Api.Contracts;

public class CreateQuoteRequest
{
    [Required, MaxLength(50)]
    public string DocumentId { get; set; } = null!;

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = null!;
}