using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quotes.Api.Application.Quotes;
using Quotes.Api.Contracts;

namespace Quotes.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/quotes")]
public class QuotesController : ControllerBase
{
    private readonly CreateQuoteHandler _handler;

    public QuotesController(CreateQuoteHandler handler) => _handler = handler;

    [HttpPost]
    public async Task<IActionResult> CreateQuote(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] CreateQuoteRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { code = "IDEMPOTENCY_KEY_REQUIRED", message = "Idempotency-Key header is required." });

        var cmd = new CreateQuoteCommand(idempotencyKey.Trim(), request.DocumentId, request.Amount, request.Currency);

        var (result, errorCode, errorMessage) = await _handler.HandleAsync(cmd, request, ct);

        if (errorCode is not null)
            return Conflict(new { code = errorCode, message = errorMessage });

        Response.Headers["Idempotency-Status"] = result!.IsReplay ? "replayed" : "created";
        return StatusCode(result.StatusCode, result.Body);
    }
}