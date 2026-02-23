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
