# Gearnix

Gearnix is an e-commerce backend for consumer electronics (phones, laptops, PC components, accessories) built around
**multi-warehouse inventory management**. The storefront is deliberately simple; the focus is on stock correctness
under concurrency, reservations with expiry, idempotent checkout and payments, and a traceable stock ledger.

> **Core promise:** stock is never oversold, and every stock change is traceable.

**Status:** work in progress — Week 1 (foundation). See the roadmap in [`Workflow.md`](Workflow.md) §14.

## Run locally

Requirements: [.NET SDK 10](https://dotnet.microsoft.com/download) and Docker with Docker Compose.

1. Create your local environment file and set a database password:

   ```bash
   cp .env.example .env
   # edit .env and set POSTGRES_PASSWORD
   ```

2. Start PostgreSQL:

   ```bash
   docker compose up -d
   ```

3. Run the API:

   ```bash
   dotnet run --project src/Api
   ```

Build and test everything with `dotnet build` and `dotnet test`.

## Live demo

_Coming in Week 5._

## Architecture

_To be written: architecture diagram and order/stock state diagram._

## Hard problems and how I solved them

_To be written as each problem (P1–P7) is implemented._

## Test results

_To be written: concurrency tests and k6 load-test numbers with machine specs._

## Tech stack

_To be written. Planned stack: [`Workflow.md`](Workflow.md) §4._

## Known limitations

_To be written. See [`Workflow.md`](Workflow.md) §11.3 and §11.7._

## What I would do next at larger scale

_To be written. See [`Workflow.md`](Workflow.md) §18._
