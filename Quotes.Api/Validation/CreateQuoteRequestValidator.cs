using FluentValidation;
using Quotes.Api.Contracts;

namespace Quotes.Api.Validation;

public class CreateQuoteRequestValidator : AbstractValidator<CreateQuoteRequest>
{
    public CreateQuoteRequestValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(999999999);
        RuleFor(x => x.Currency).NotEmpty().Length(3).Matches("^[A-Za-z]{3}$");
    }
}