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

```json
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
    "documentId": "DOC-12345",
    "amount": 1500,
    "currency": "CLP",
    "status": "ISSUED",
    "createdAt": "2026-02-22T22:10:29.991Z"
  }
}```