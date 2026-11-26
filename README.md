# Gearnix

[![CI](https://github.com/hienbn1910/gearnix/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/hienbn1910/gearnix/actions/workflows/ci.yml)

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
   # edit .env: set POSTGRES_PASSWORD, and change POSTGRES_PORT if 5432 is already in use
   ```

2. Start PostgreSQL:

   ```bash
   docker compose up -d
   ```

3. Store the connection string in user-secrets (kept outside the repository), using the values from `.env`:

   ```bash
   dotnet user-secrets set "Database:ConnectionString" \
     "Host=127.0.0.1;Port=<POSTGRES_PORT>;Database=gearnix;Username=gearnix;Password=<POSTGRES_PASSWORD>" \
     --project src/Api
   ```

4. Install the repository's tools and apply the database migrations:

   ```bash
   dotnet tool restore
   dotnet ef database update --project src/Api
   ```

5. Set the demo account passwords (any values you like; kept outside the repository) and seed the roles and the
   `staff@demo.local` / `admin@demo.local` accounts. Seeding is idempotent, so it can be run again safely:

   ```bash
   dotnet user-secrets set "Seed:StaffPassword" "<staff password>" --project src/Api
   dotnet user-secrets set "Seed:AdminPassword" "<admin password>" --project src/Api
   dotnet run --project src/Api -- seed
   ```

6. Run the API and check it:

   ```bash
   dotnet run --project src/Api
   curl http://localhost:5296/api/health/ready   # Healthy
   ```

   API documentation (Development only): http://localhost:5296/api/docs. Every `POST`/`PUT`/`PATCH`/`DELETE` request
   needs the header `X-CSRF: 1`.

Build and test everything with `dotnet build` and `dotnet test`. Integration tests need Docker running: they start their own PostgreSQL container.

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
