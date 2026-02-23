## Point 1 — API & Contract (`openapi.yaml`)

### Prompt 1
Create `openapi.yaml` (OpenAPI 3.0.3) for `POST /api/v1/quotes`. Require `Idempotency-Key` header, include JWT Bearer security, define request/response schemas, and document responses 201/400/401/409/422/503. Add `Idempotency-Status` response header (`created` | `replayed`) on 201.

### AI Output 1 
```yaml
paths:
  /api/v1/quotes:
    post:
      tags: [Quotes]
      summary: Issue a quote (idempotent)
      description: >
        Issues a quote. This operation is idempotent when the client provides
        a stable Idempotency-Key per logical request.
        If the same Idempotency-Key is used again with the exact same request body,
        the API returns the stored response (no duplicate quote is created).
        If the same Idempotency-Key is reused with a different request body,
        the API returns 409 Conflict.
      operationId: createQuote
      security:
        - bearerAuth: []
      parameters:
        - name: Idempotency-Key
          in: header
          required: true
          description: >
            A client-generated unique key to enforce idempotency for this request.
            Recommended: UUID v4.
            Reuse the same key only when retrying the same logical request.
          schema:
            type: string
            minLength: 8
            maxLength: 128
          example: "7f3c1b4b-8a8f-4b73-b7c1-3b2b0a2f4a7e"
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: "#/components/schemas/QuoteCreateRequest"
            examples:
              example1:
                value:
                  documentId: "DOC-123456"
                  amount: 19990.50
                  currency: "CLP"
      responses:
        "201":
          description: Quote issued successfully (created or replayed)
          headers:
            Idempotency-Status:
              description: Indicates whether the response was created or replayed from storage.
              schema:
                type: string
                enum: [created, replayed]
              example: created
          content:
            application/json:
              schema:
                $ref: "#/components/schemas/QuoteResponse"
              examples:
                ok:
                  value:
                    id: "a3b2c1d0-1111-2222-3333-444455556666"
                    documentId: "DOC-123456"
                    amount: 19990.50
                    currency: "CLP"
                    status: "ISSUED"
                    createdAt: "2026-02-21T12:34:56Z"
        "400":
          description: Bad Request (validation error)
          content:
            application/json:
              schema:
                $ref: "#/components/schemas/ErrorResponse"
              examples:
                missingHeader:
                  value:
                    code: "BAD_REQUEST"
                    message: "Idempotency-Key header is required."
                invalidBody:
                  value:
                    code: "VALIDATION_ERROR"
                    message: "One or more fields are invalid."
                    details:
                      - field: "amount"
                        issue: "must be greater than 0"
        "401":
          description: Unauthorized (missing/invalid token)
          content:
            application/json:
              schema:
                $ref: "#/components/schemas/ErrorResponse"
              examples:
                unauthorized:
                  value:
                    code: "UNAUTHORIZED"
                    message: "Authentication required."
        "403":
          description: Forbidden (insufficient permissions)
          content:
            application/json:
              schema:
                $ref: "#/components/schemas/ErrorResponse"
              examples:
                forbidden:
                  value:
                    code: "FORBIDDEN"
                    message: "You do not have permission to access this resource."
        "409":
          description: Conflict (idempotency key reused with different request body)
          content:
            application/json:
              schema:
                $ref: "#/components/schemas/ErrorResponse"
              examples:
                idempotencyConflict:
                  value:
                    code: "IDEMPOTENCY_CONFLICT"
                    message: "Idempotency-Key was already used with a different request payload."
        "500":
          description: Internal Server Error
          content:
            application/json:
              schema:
                $ref: "#/components/schemas/ErrorResponse"
```

---

## Point 2 — Service Implementation (Resilience, Logging, Metrics, Containers, k8s)

### Prompt 1
Provide a minimal ASP.NET Core controller implementation for `POST /api/v1/quotes` that:
- requires JWT (`[Authorize]`)
- validates `Idempotency-Key` header (400 if missing)
- returns 409 with an error code if the same idempotency key is reused with a different payload
- emits `Idempotency-Status` header (`created` | `replayed`)

### AI Output 1 
```csharp
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
```

### Prompt 2
Draft a `docker-compose.yml` running PostgreSQL and the API. Expose API on host port 5033 and Postgres on host port 55432.

### AI Output 2 (saved as `docker-compose.yml`)
```yaml
services:
  postgres:
    image: postgres:16
    container_name: quotes-postgres
    environment:
      POSTGRES_DB: quotesdb
      POSTGRES_USER: quotesuser
      POSTGRES_PASSWORD: quotespass
    ports:
      - "55432:5432"
    volumes:
      - quotes_pgdata:/var/lib/postgresql/data

  quotes-api:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: quotes-api
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=quotesdb;Username=quotesuser;Password=quotespass
      Jwt__Issuer: QuotesAggregator
      Jwt__Audience: QuotesAggregatorClients
      Jwt__Key: YOUR_SUPER_SECRET_KEY_44_CHARS_MIN___________
    ports:
      - "5033:8080"
    depends_on:
      - postgres

volumes:
  quotes_pgdata:
```

### Prompt 3
Draft Kubernetes manifests under `k8s/` for API + Postgres (namespace, deployments, services, and a secret file for API configuration).

### AI Output 3 
A file set was generated with the following paths:
- `k8s/namespace.yaml`
- `k8s/deployment-api.yaml`
- `k8s/service-api.yaml`
- `k8s/deployment-postgres.yaml`
- `k8s/service-postgres.yaml`
- `k8s/secret-quotes-api.yaml`

---

## Point 3 — CI/CD 

### Prompt 1
Create `azure-pipelines.yml` with stages:
- Build (restore + build)
- Test (dotnet test)
- Container build
- Integration stage that runs `docker compose up`, waits for `/health`, runs integration tests, and always tears down compose.

### AI Output 1 
```yaml
trigger:
- main

variables:
  buildConfiguration: 'Release'
  dotnetVersion: '8.x'

stages:

# BUILD & UNIT TEST
- stage: Build
  displayName: 'Build & Unit Tests'
  jobs:
  - job: BuildJob
    pool:
      vmImage: 'ubuntu-latest'
    steps:

    - task: UseDotNet@2
      inputs:
        packageType: 'sdk'
        version: '$(dotnetVersion)'

    - script: dotnet restore
      displayName: 'Restore'

    - script: dotnet build --configuration $(buildConfiguration) --no-restore
      displayName: 'Build'

    - script: dotnet test --configuration $(buildConfiguration) --no-build --collect:"XPlat Code Coverage"
      displayName: 'Run Unit Tests'

    - task: PublishTestResults@2
      inputs:
        testResultsFormat: 'VSTest'
        testResultsFiles: '**/*.trx'
        failTaskOnFailedTests: true

    - task: PublishCodeCoverageResults@1
      inputs:
        codeCoverageTool: 'Cobertura'
        summaryFileLocation: '$(System.DefaultWorkingDirectory)/**/coverage.cobertura.xml'


# DOCKER BUILD
- stage: Container
  displayName: 'Build Docker Image'
  dependsOn: Build
  jobs:
  - job: DockerJob
    pool:
      vmImage: 'ubuntu-latest'
    steps:

    - script: docker build -t quotes-api:latest .
      displayName: 'Build Docker Image'



# INTEGRATION TESTS
- stage: Integration
  displayName: 'Integration Tests'
  dependsOn: Container
  jobs:
  - job: IntegrationJob
    pool:
      vmImage: 'ubuntu-latest'
    steps:

    - task: UseDotNet@2
      inputs:
        packageType: 'sdk'
        version: '$(dotnetVersion)'

    - script: docker compose up -d --build
      displayName: 'Start docker-compose stack'

    - script: |
        echo "Waiting for API health..."
        for i in {1..30}; do
          if curl -fsS http://localhost:5033/health > /dev/null; then
            echo "API is healthy"
            exit 0
          fi
          sleep 2
        done
        echo "API did not become healthy in time"
        exit 1
      displayName: 'Wait for /health'

    - script: dotnet test --configuration $(buildConfiguration) --filter "FullyQualifiedName~Integration"
      displayName: 'Run Integration Tests'

    - script: docker compose down -v
      displayName: 'Stop docker-compose'
      condition: always()
```

---

## Point 4 — Integration Platform Design (`events/`)

### Prompt 1
Draft `events/QuoteIssued.schema.json` for a `QuoteIssued` event envelope including metadata (`specVersion`, `eventType`, `eventVersion`, `eventId`, `occurredAt`), producer, trace correlation, idempotency hints, and business payload (`data`).

### AI Output 1 (saved as `events/QuoteIssued.schema.json`)
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "urn:quotes-aggregator:events:QuoteIssued:1.0",
  "title": "QuoteIssued",
  "type": "object",
  "additionalProperties": false,
  "required": ["specVersion", "eventType", "eventVersion", "eventId", "occurredAt", "producer", "data"],
  "properties": {
    "specVersion": {
      "type": "string",
      "description": "Envelope specification version for this event contract.",
      "const": "1.0"
    },
    "eventType": {
      "type": "string",
      "description": "Business event name.",
      "const": "QuoteIssued"
    },
    "eventVersion": {
      "type": "string",
      "description": "Event contract version.",
      "const": "1.0"
    },
    "eventId": {
      "type": "string",
      "description": "Unique identifier for this event instance (UUID).",
      "format": "uuid"
    },
    "occurredAt": {
      "type": "string",
      "description": "UTC timestamp when the event occurred.",
      "format": "date-time"
    },
    "producer": {
      "type": "object",
      "additionalProperties": false,
      "required": ["service", "environment"],
      "properties": {
        "service": { "type": "string", "description": "Producer service name." },
        "environment": { "type": "string", "description": "Environment name (dev/test/prod)." }
      }
    },
    "trace": {
      "type": "object",
      "additionalProperties": false,
      "required": ["correlationId"],
      "properties": {
        "correlationId": { "type": "string", "description": "Correlation identifier for cross-service tracing." },
        "requestId": { "type": "string", "description": "Optional request identifier." }
      }
    },
    "idempotency": {
      "type": "object",
      "additionalProperties": false,
      "required": ["key", "requestHash"],
      "properties": {
        "key": { "type": "string", "description": "Idempotency-Key used at the API level (if provided)." },
        "requestHash": { "type": "string", "description": "SHA-256 hash of the normalized request payload (hex)." }
      }
    },
    "data": {
      "type": "object",
      "additionalProperties": false,
      "required": ["quoteId", "documentId", "amount", "currency", "status", "createdAt"],
      "properties": {
        "quoteId": { "type": "string", "format": "uuid", "description": "Quote identifier." },
        "documentId": { "type": "string", "minLength": 1, "maxLength": 64, "description": "Business document identifier." },
        "amount": { "type": "number", "description": "Quote amount.", "minimum": 0 },
        "currency": { "type": "string", "description": "ISO-like currency code (e.g., CLP, USD).", "minLength": 3, "maxLength": 3 },
        "status": { "type": "string", "description": "Quote status.", "enum": ["ISSUED"] },
        "createdAt": { "type": "string", "format": "date-time", "description": "UTC timestamp when the quote was created." }
      }
    }
  }
}
```

### Prompt 2
Draft `events/README.md` to describe the event interface for MuleSoft or Azure Integration Services, including reliability semantics: at-least-once, retries, DLQ, ordering, and schema evolution rules. Include an example payload.

### AI Output 2 
markdown
# QuoteIssued Event Interface (Integration Platform Design)

## Purpose

`QuoteIssued` is emitted when a quote is successfully created (status `ISSUED`).  
The event enables downstream systems (billing, CRM, auditing, analytics, reporting)
to consume quote data asynchronously in a decoupled and scalable manner.

---

## Transport Options

This contract is transport-agnostic and can be implemented using:

- **Azure Integration Services**
  - Azure Service Bus Topic (recommended)
  - Azure Event Grid
- **MuleSoft**
  - Anypoint MQ
  - JMS
  - Kafka connectors

The message structure remains identical regardless of the transport layer.

---

## Event Envelope

Each message contains the following sections:

### Metadata
- `specVersion`
- `eventType`
- `eventVersion`
- `eventId`
- `occurredAt`

### Producer Identity
- `producer.service`
- `producer.environment`

### Observability
- `trace.correlationId`

### Idempotency Hints
- `idempotency.key`
- `idempotency.requestHash`

### Business Payload
- `data`

---

## Schema

JSON Schema file: `QuoteIssued.schema.json`  
Schema version: Draft 2020-12

The schema defines structure, required fields, and validation constraints.

---

# Reliability & Delivery Semantics

## Delivery Model

`QuoteIssued` MUST be delivered using **at-least-once semantics**.

Implications:

- The same event MAY be delivered more than once.
- Consumers MUST implement idempotent processing.
- Producers MAY retry publishing in case of transient failures.
- Exactly-once delivery is NOT assumed at transport level.

---

## Idempotency Strategy

To ensure safe reprocessing:

- `eventId` is globally unique and MUST be used to detect duplicates.
- Consumers SHOULD persist processed `eventId` values in a durable store.
- Alternatively, `data.quoteId` MAY be used as a business-level idempotency key.

If a duplicate event is detected:
- The consumer MUST ignore it.
- No side effects MUST be executed.

---

## Retry Policy (Producer Side)

When publishing to the message broker:

- Transient failures MUST trigger retries.
- Exponential backoff SHOULD be applied (e.g., 1s → 2s → 5s → 10s).
- A maximum retry limit MUST be configured.
- Retries MUST NOT generate a new `eventId`.

---

## Dead Letter Queue (DLQ)

If an event cannot be delivered after configured retry attempts:

- It MUST be routed to a Dead Letter Queue (DLQ).
- DLQ messages MUST include:
  - Original payload
  - Failure reason
  - Timestamp of last retry

Operational teams SHOULD monitor DLQ depth.

---

## Ordering Guarantees

Global ordering is NOT guaranteed.

If ordering is required:

- Events SHOULD be partitioned by `documentId`.
- The broker partition key SHOULD be set to `documentId`.

Consumers MUST NOT assume ordering across partitions.

---

## Schema Evolution

- Backward-compatible changes (adding optional fields) are allowed.
- Required field removals or type changes are considered breaking changes.
- Breaking changes MUST introduce a new `eventVersion` (e.g., `2.0`).
- Producers and consumers MUST support version coexistence during migration.

---

# Example Payload

json
{
  "specVersion": "1.0",
  "eventType": "QuoteIssued",
  "eventVersion": "1.0",
  "eventId": "4c3b8d23-9a5c-4f9c-bf70-90c0d92d7e9c",
  "occurredAt": "2026-02-22T22:10:30.123Z",
  "producer": {
    "service": "Quotes.Api",
    "environment": "local"
  },
  "trace": {
    "correlationId": "c8a6d0d7f6c34c1d9b1b0a0e2a9d61d8"
  },
  "idempotency": {
    "key": "idem-123",
    "requestHash": "b2d6a0...<sha256hex>...9f"
  },
  "data": {
    "quoteId": "f4c7db11-d2b9-4e3a-9831-640ce3d2ec3d",



### Point 5 — SQL (`sql/last_quote_per_documentId.sql`)

### Prompt 1
Write PostgreSQL SQL to obtain the last quote per `DocumentId` for a list of up to 500 ids using an array parameter (`ANY ($1::text[])`). Include indexing guidance aligned to the query.

### AI Output 1 
```sql
-- last_quote_per_documentId.sql
-- Task 5 (SQL): Fetch the most recent quote per documentId for a list of up to 500 documentIds.
-- Model verified from the solution:
--   Table: "Quotes"
--   Columns: "Id" (uuid PK), "DocumentId" (varchar(50)), "Amount" (numeric(18,2)),
--            "Currency" (varchar(3)), "Status" (varchar(20)), "CreatedAt" (timestamptz)

-- ============================================================
-- INPUT PATTERN (recommended): pass the 500 ids as a text[] parameter
-- ============================================================
-- Example (psql):
--   -- \set document_ids ARRAY['DOC-1','DOC-2',...]
-- Or from an app:
--   -- NpgsqlParameter("document_ids", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = idsArray }

-- ============================================================
-- (A) PostgreSQL-native optimal query: DISTINCT ON
-- ============================================================
-- Notes:
--  - DISTINCT ON is PostgreSQL-specific and typically the fastest/simplest way to do "latest per group".
--  - Tie-breaker included ("Id" DESC) to make result deterministic when CreatedAt is equal.
--  - Uses = ANY($1) to avoid building huge dynamic SQL for IN (...) lists.
--
-- Parameters:
--   $1 :: text[]   -- document ids
SELECT DISTINCT ON (q."DocumentId")
       q."Id",
       q."DocumentId",
       q."Amount",
       q."Currency",
       q."Status",
       q."CreatedAt"
FROM "Quotes" AS q
WHERE q."DocumentId" = ANY ($1::text[])
ORDER BY q."DocumentId", q."CreatedAt" DESC, q."Id" DESC;

-- ============================================================
-- (B) Portable alternative: ROW_NUMBER() window function
-- ============================================================
-- Works across most RDBMS (PostgreSQL, SQL Server, Oracle, etc.).
-- Parameters:
--   $1 :: text[]   -- document ids
WITH ranked AS (
    SELECT
        q."Id",
        q."DocumentId",
        q."Amount",
        q."Currency",
        q."Status",
        q."CreatedAt",
        ROW_NUMBER() OVER (
            PARTITION BY q."DocumentId"
            ORDER BY q."CreatedAt" DESC, q."Id" DESC
        ) AS rn
    FROM "Quotes" AS q
    WHERE q."DocumentId" = ANY ($1::text[])
)
SELECT
    "Id",
    "DocumentId",
    "Amount",
    "Currency",
    "Status",
    "CreatedAt"
FROM ranked
WHERE rn = 1;

-- ============================================================
-- INDEXING STRATEGY
-- ============================================================
-- Current model already creates:
--   CREATE INDEX ... ON "Quotes" ("DocumentId", "CreatedAt");
-- Postgres can use this index efficiently and scan "CreatedAt" backwards, but we can optimize further.

-- 1) Best-practice index for "latest per documentId" lookups:
--    - Order by "CreatedAt" DESC to match the query ordering.
--    - INCLUDE the projected columns to enable index-only scans (PostgreSQL 11+).
--    - Use CONCURRENTLY in production to avoid long blocking (requires running outside a transaction).
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Quotes_DocumentId_CreatedAt_DESC"
ON "Quotes" ("DocumentId", "CreatedAt" DESC)
INCLUDE ("Id", "Amount", "Currency", "Status");

-- 2) If you only need (Id, DocumentId, CreatedAt), keep INCLUDE minimal:
--    INCLUDE ("Id")  -- and select fewer columns for even better I/O.

-- 3) If the table grows very large and you routinely query only recent data, consider:
--    - BRIN index on "CreatedAt" for time-range scans (different access pattern), or
--    - Table partitioning by time if operationally justified.
```

---

## Point 6 — Quality & Performance (Unit + Integration Tests, k6)

### Prompt 1
Generate unit tests for create-quote idempotency:
- first call creates and stores idempotency
- same key + same payload replays stored response
- same key + different payload returns conflict error code

### AI Output 1 
```csharp
public class CreateQuoteHandler_Tests
{
    [Fact]
    public async Task HandleAsync_FirstCall_CreatesQuoteAndStoresIdempotency()
    {
        await using var db = NewDb();
        var idem = new IdempotencyService(db);
        var handler = new CreateQuoteHandler(db, idem);

        var request = new CreateQuoteRequest { DocumentId = "DOC-U1", Amount = 99.9m, Currency = "clp" };
        var cmd = new CreateQuoteCommand("key-u1", request.DocumentId, request.Amount, request.Currency);

        var (result, errorCode, _) = await handler.HandleAsync(cmd, request, CancellationToken.None);

        errorCode.Should().BeNull();
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(201);
        result.IsReplay.Should().BeFalse();

        (await db.Quotes.CountAsync()).Should().Be(1);
        (await db.IdempotencyRecords.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_SameKeySamePayload_ReplaysStoredResponse()
    {
        await using var db = NewDb();
        var idem = new IdempotencyService(db);
        var handler = new CreateQuoteHandler(db, idem);

        var request = new CreateQuoteRequest { DocumentId = "DOC-U2", Amount = 10m, Currency = "usd" };
        var cmd = new CreateQuoteCommand("key-u2", request.DocumentId, request.Amount, request.Currency);

        var first = await handler.HandleAsync(cmd, request, CancellationToken.None);
        var second = await handler.HandleAsync(cmd, request, CancellationToken.None);

        first.errorCode.Should().BeNull();
        second.errorCode.Should().BeNull();
        second.result!.IsReplay.Should().BeTrue();
        second.result.Body.Id.Should().Be(first.result!.Body.Id);

        (await db.Quotes.CountAsync()).Should().Be(1);
        (await db.IdempotencyRecords.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_SameKeyDifferentPayload_ReturnsConflict()
    {
        await using var db = NewDb();
        var idem = new IdempotencyService(db);
        var handler = new CreateQuoteHandler(db, idem);

        var req1 = new CreateQuoteRequest { DocumentId = "DOC-U3", Amount = 10m, Currency = "clp" };
        var req2 = new CreateQuoteRequest { DocumentId = "DOC-U3", Amount = 11m, Currency = "clp" };

        var cmd1 = new CreateQuoteCommand("key-u3", req1.DocumentId, req1.Amount, req1.Currency);
        var cmd2 = new CreateQuoteCommand("key-u3", req2.DocumentId, req2.Amount, req2.Currency);

        var first = await handler.HandleAsync(cmd1, req1, CancellationToken.None);
        var second = await handler.HandleAsync(cmd2, req2, CancellationToken.None);

        first.errorCode.Should().BeNull();
        second.errorCode.Should().Be("IDEMPOTENCY_KEY_REUSE_CONFLICT");

        (await db.Quotes.CountAsync()).Should().Be(1);
        (await db.IdempotencyRecords.CountAsync()).Should().Be(1);
    }

    private static QuotesDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<QuotesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new QuotesDbContext(opts);
    }
}
```

### Prompt 2
Generate integration tests for `POST /api/v1/quotes` using `WebApplicationFactory`:
- without JWT returns 401
- without Idempotency-Key returns 400
- replay returns same response
- conflict returns 409 and error code `IDEMPOTENCY_KEY_REUSE_CONFLICT`

### AI Output 2 (saved as `Quotes.Api.Tests/Integration/QuotesApi_CreateQuote_Tests.cs`)
```csharp
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
```

### Prompt 3
Generate a `CustomWebApplicationFactory` that overrides JWT settings to produce predictable tokens and uses an in-memory EF Core database for integration tests.

### AI Output 3 
```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override JWT settings for predictable test tokens
            var overrides = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "THIS_IS_A_TEST_KEY_32_CHARS_MINIMUM!!",
                ["Jwt:Issuer"] = "quotes-api-tests",
                ["Jwt:Audience"] = "quotes-api-tests"
            };
            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            // Replace Npgsql DbContext with InMemory DbContext for integration tests
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<QuotesDbContext>));
            if (dbContextDescriptor is not null)
                services.Remove(dbContextDescriptor);

            services.AddDbContext<QuotesDbContext>(opt =>
                opt.UseInMemoryDatabase("QuotesDb_Test"));
        });
    }
}
```

### Prompt 4
Draft `k6/smoke_create_quote.js` that:
- uses `BASE_URL` (default `http://localhost:5033`)
- obtains a JWT from `/api/v1/auth/token`
- sends POST `/api/v1/quotes` with `Authorization` and `Idempotency-Key`
- asserts status 201 and response contains an `id` field
- includes thresholds

### AI Output 4 
```javascript
export const options = {
  vus: 1,
  duration: '30s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<800'],
  },
};

// Default aligns with docker-compose host port mapping (5033:8080)
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5033';

export function setup() {
  // Obtain JWT token from the API itself
  const res = http.post(`${BASE_URL}/api/v1/auth/token`, null);
  check(res, { 'token status 200': (r) => r.status === 200 });
  const body = res.json();
  return { token: body.access_token };
}

export default function (data) {
  const token = data.token;

  const payload = JSON.stringify({
    documentId: `DOC-SMOKE-${__VU}`,
    amount: 123.45,
    currency: 'CLP',
  });

  const idempotencyKey = `smoke-${__VU}-${__ITER}-${Date.now()}`;

  const res = http.post(`${BASE_URL}/api/v1/quotes`, payload, {
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
      'Idempotency-Key': idempotencyKey,
    },
  });

  check(res, {
    'status is 201': (r) => r.status === 201,
    'has id': (r) => !!r.json('id'),
  });

  sleep(1);
}
```

### Prompt 5
Draft `k6/load_create_quote.js` that:
- uses `BASE_URL` (default `http://localhost:5033`)
- obtains a JWT from `/api/v1/auth/token`
- ramps VUs, defines thresholds, posts to `/api/v1/quotes` with unique idempotency keys

### AI Output 5 
```javascript
export const options = {
  scenarios: {
    load: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 20 },
        { duration: '60s', target: 50 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1200'],
  },
};

// Default aligns with docker-compose host port mapping (5033:8080)
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5033';

export function setup() {
  const res = http.post(`${BASE_URL}/api/v1/auth/token`, null);
  check(res, { 'token status 200': (r) => r.status === 200 });
  const body = res.json();
  return { token: body.access_token };
}

export default function (data) {
  const token = data.token;

  // Use a lot of distinct documentIds to exercise indexing and DB writes
  const docId = `DOC-LOAD-${__VU}-${__ITER % 1000}`;

  const payload = JSON.stringify({
    documentId: docId,
    amount: 10.0 + (__ITER % 100),
    currency: 'CLP',
  });

  // IMPORTANT: unique idempotency key per request, otherwise replays will skew the load test
  const idempotencyKey = `load-${__VU}-${__ITER}-${Date.now()}`;

  const res = http.post(`${BASE_URL}/api/v1/quotes`, payload, {
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
      'Idempotency-Key': idempotencyKey,
    },
  });

  check(res, {
    'status is 201': (r) => r.status === 201,
  });

  sleep(0.2);
}
```

---

## Runbook (`RUNBOOK.md`)

### Prompt 1
Draft `RUNBOOK.md` with step-by-step commands to:
- start the stack with docker compose
- verify `/health` and `/metrics`
- run `dotnet test`
- run k6 smoke and load scripts against `http://localhost:5033`
- tear down docker compose cleanly

### AI Output 1 
```markdown
# RUNBOOK --- Quotes Aggregator

This document explains how to build, run, test, and load test the Quotes
Aggregator service locally.

------------------------------------------------------------------------

## Prerequisites

-   .NET SDK 8.x
-   Docker Desktop (or Docker Engine) with Docker Compose v2
-   k6 installed
    -   Windows (choco): `choco install k6`
    -   macOS (brew): `brew install k6`
    -   Linux: see official k6 documentation

------------------------------------------------------------------------

## Project Overview

The solution includes:

-   ASP.NET Core 8 API
-   PostgreSQL (via Docker Compose)
-   Prometheus metrics endpoint
-   Unit and Integration tests
-   k6 performance scripts
-   Azure DevOps pipeline

------------------------------------------------------------------------

## Environment

Docker Compose exposes the API on:

http://localhost:5033

PostgreSQL runs inside the Docker network.

------------------------------------------------------------------------

# 1. Start the Application Stack

From repository root:

``` bash
docker compose up -d --build
```

------------------------------------------------------------------------

# 2. Verify Service Readiness

Health endpoint:

``` bash
curl -f http://localhost:5033/health
```

Metrics endpoint:

``` bash
curl -f http://localhost:5033/metrics
```

If health fails, inspect logs:

``` bash
docker compose logs -f
```

------------------------------------------------------------------------

# 3. Run Tests

Run all tests:

``` bash
dotnet test
```

Run only integration tests:

``` bash
dotnet test --filter "FullyQualifiedName~Integration"
```

------------------------------------------------------------------------

# 4. Run Performance Tests (k6)

Smoke test:

``` bash
BASE_URL=http://localhost:5033 k6 run k6/smoke_create_quote.js
```

Load test:

``` bash
BASE_URL=http://localhost:5033 k6 run k6/load_create_quote.js
```

------------------------------------------------------------------------

# 5. Stop and Clean Up

``` bash
docker compose down -v
```

------------------------------------------------------------------------

# Troubleshooting

## Port already in use

Ensure port 5033 is free or update docker-compose.yml mapping.

## 401 Unauthorized

The endpoint requires JWT authentication. Include: - Authorization:
Bearer `<token>`{=html} - Idempotency-Key header

## Database not ready

Wait a few seconds and retry the health endpoint.

------------------------------------------------------------------------

# CI/CD Notes

The Azure DevOps pipeline:

1.  Builds the solution
2.  Runs tests
3.  Builds the Docker image
4.  Starts docker compose
5.  Waits for /health
6.  Executes integration tests
7.  Tears down compose environment

------------------------------------------------------------------------

# Observability

-   Health: `/health`
-   Metrics: `/metrics` (Prometheus compatible)
-   Structured logs include correlation identifiers.

------------------------------------------------------------------------

This runbook ensures reproducible local execution and validation of the
Quotes Aggregator service.
```
