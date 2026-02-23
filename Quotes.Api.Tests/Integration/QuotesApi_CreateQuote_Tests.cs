using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Quotes.Api.Contracts;
using Xunit;

namespace Quotes.Api.Tests.Integration;

public class QuotesApi_CreateQuote_Tests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public QuotesApi_CreateQuote_Tests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateQuote_WithoutJwt_Returns401()
    {
        var req = new CreateQuoteRequest { DocumentId = "DOC-1", Amount = 10.5m, Currency = "clp" };

        using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/quotes")
        {
            Content = JsonContent.Create(req)
        };
        msg.Headers.Add("Idempotency-Key", "abc-1");

        var res = await _client.SendAsync(msg);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateQuote_WithoutIdempotencyKey_Returns400()
    {
        var token = await GetTokenAsync();

        var req = new CreateQuoteRequest { DocumentId = "DOC-2", Amount = 20m, Currency = "usd" };
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/quotes")
        {
            Content = JsonContent.Create(req)
        };
        msg.Headers.Add("Authorization", $"Bearer {token}");

        var res = await _client.SendAsync(msg);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateQuote_SameIdempotencyKeySamePayload_ReplaysSameResponse()
    {
        var token = await GetTokenAsync();

        var req = new CreateQuoteRequest { DocumentId = "DOC-3", Amount = 30m, Currency = "clp" };
        var key = "idem-123";

        var first = await PostQuoteAsync(token, key, req);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await first.Content.ReadFromJsonAsync<QuoteResponse>();
        firstBody.Should().NotBeNull();

        var second = await PostQuoteAsync(token, key, req);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondBody = await second.Content.ReadFromJsonAsync<QuoteResponse>();

        secondBody!.Id.Should().Be(firstBody!.Id);
        secondBody.CreatedAt.Should().Be(firstBody.CreatedAt);
        secondBody.DocumentId.Should().Be(firstBody.DocumentId);
        secondBody.Amount.Should().Be(firstBody.Amount);
        secondBody.Currency.Should().Be(firstBody.Currency);
    }

    [Fact]
    public async Task CreateQuote_SameIdempotencyKeyDifferentPayload_Returns409()
    {
        var token = await GetTokenAsync();

        var key = "idem-456";
        var req1 = new CreateQuoteRequest { DocumentId = "DOC-4", Amount = 40m, Currency = "clp" };
        var req2 = new CreateQuoteRequest { DocumentId = "DOC-4", Amount = 41m, Currency = "clp" };

        var first = await PostQuoteAsync(token, key, req1);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await PostQuoteAsync(token, key, req2);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var conflict = await second.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        conflict!["code"].Should().Be("IDEMPOTENCY_KEY_REUSE_CONFLICT");
    }

    private async Task<string> GetTokenAsync()
    {
        var res = await _client.PostAsync("/api/v1/auth/token", content: null);
        res.EnsureSuccessStatusCode();

        var payload = await res.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return payload!["access_token"]; 
    }

    private Task<HttpResponseMessage> PostQuoteAsync(string token, string idempotencyKey, CreateQuoteRequest request)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/quotes")
        {
            Content = JsonContent.Create(request)
        };
        msg.Headers.Add("Authorization", $"Bearer {token}");
        msg.Headers.Add("Idempotency-Key", idempotencyKey);
        return _client.SendAsync(msg);
    }
}
