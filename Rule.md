# Rule.md — Coding Rules for AI Agents

> Read this file before writing or changing any code.
> After coding, self-review against these rules and output the report in section 12.
> This file is the repo's coding standard: the `code-review` skill reviews every diff against it.

## 0. Priority

When rules conflict: **correctness & security > data consistency > performance > style**.
If a rule cannot be followed, say so explicitly and explain why. Never skip a rule silently.

## 1. Workflow

The size of the task decides the flow:

- **Small task** (typo, rename, config value, one file, no behavior change):
  code → build & test → self-review report. No spec issue needed.
- **Normal task** (every roadmap task in `Workflow.md` §14, any bug fix, anything touching stock, money or auth):
  the full flow below.

The flow uses the installed agent skills (mattpocock/skills). Skill names are in `backticks`;
user-invoked skills start with `/`.

### Full flow

```
/grill-with-docs → /to-spec (GitHub issue) → Plan → ✋ approval
→ for each vertical slice: [red → green] (tdd)
→ full test run → /code-review → fix & refactor → test again → commit
→ ✋ human pushes → PR (pr) → CI → ✋ human reviews & merges
```

**Spec**

1. **Grill** — the human runs `/grill-with-docs` for the task: the agent interviews the human until every
   decision is settled, and records new terms in `GLOSSARY.md` and decisions in `docs/adr/`.
2. **Spec issue** — the human runs `/to-spec`: the agent writes the spec as a **GitHub issue**,
   agrees the **test seams** with the human, and adds in "Further Notes":
   `Task: W?-??` and the relevant `Workflow.md` sections.
   Then add the issue link next to the task in `Workflow.md` §14.
   If the spec contradicts `Workflow.md`, or something is unclear, ask. Never invent requirements.

**Plan**

3. **Plan** — read the issue, this file, the related `Workflow.md` sections, ADRs and code. Propose:
   files to touch, approach, DB/API changes, risks, and the **vertical slices** in order
   (each slice is a thin end-to-end path that can be tested on its own).
   **STOP and wait for human approval before coding.**
   A large ⭐ task may be split into sub-issues with `/to-tickets` (human decides).

**Build — for each slice, follow the `tdd` skill**

4. **Red** — write **one** failing test at an agreed seam. Expected values come from the spec, never recomputed
   the way the code computes them.
5. **Green** — the minimum code that makes that test pass, following this file.
   If the code would contain repeated DB calls, loops calling the DB or external services,
   or N+1 patterns: **STOP and propose an optimization before writing it.**
6. Repeat 4–5 until the slice is done, then start the next slice.
   **Never write all tests first and all code afterwards.**

**Verify**

7. **Run** — `dotnet build` and the full `dotnet test` (plus `npm run lint` and `npm run build` if the frontend changed).
8. **Fix** — fix the code, not the tests. A test may be changed only if it contradicts the spec; explain why.
   After **3 failed attempts** on the same failure, stop and report what was tried
   (use the `diagnosing-bugs` skill for hard or flaky failures).
9. **Review** — run `code-review` against `main`: one reviewer checks the diff against this file (standards),
   another against the issue (spec). Fix every hard violation; discuss judgement calls with the human.
10. **Refactor** — only code touched by this task, no behavior change. Run the full tests again afterwards.

**Deliver**

11. **Commit** — small commits on the task branch, each one building and passing tests.
    Format: `type(module): summary (#issue)`, e.g. `feat(inventory): reserve stock atomically (#14)`.
    Exception: automated Dependabot pull requests use `chore(deps): …` and carry no issue number.
12. **Hand over** — post the self-review report (section 12) and a PR body written with the `pr` skill,
    including `Closes #<issue>`. Then **stop**.
13. **The human pushes** the branch. After that the agent may open the PR with `gh pr create`.
    CI must pass. **The human reviews and merges.**

### Always

- Make small, focused changes. Do not modify unrelated files or reformat untouched code.
- Never delete, skip, or weaken a test to make it pass.
- Never commit to `main`, push, force-push, or merge a PR (`gh pr merge`). The git guardrail hook blocks most of these;
  do not try to work around it.
- Never claim build or tests pass without running them.

## 2. Project structure

```
src/
  Api/
    Program.cs
    Modules/
      <Feature>/                    # Catalog, Inventory, Ordering, Payment, Identity
        <Feature>Controller.cs
        <Feature>Service.cs
        <Feature>Repository.cs
        Dtos/                       # request/response records
        Validators/                 # FluentValidation validators
        Entities/                   # EF Core entities of this module
    Common/                         # errors, auth, logging, shared helpers
    Data/
      AppDbContext.cs
      Configurations/               # IEntityTypeConfiguration<T>
      Migrations/
  web/
    storefront/                     # React app for customers
    admin/                          # React app for warehouse staff / admin
tests/
  Api.UnitTests/
  Api.IntegrationTests/
  load/                             # k6 scripts
```

- Folders and files use PascalCase. File name = class name (`InventoryService.cs`, not `Service.cs`).
- One public type per file.
- **Controller**: HTTP only — routing, auth attributes, call service, map result to HTTP response.
  No business logic, no `DbContext`.
- **Service**: business rules, transactions, orchestration. No HTTP types (`HttpContext`, `IActionResult`).
- **Repository**: EF Core queries only, no business rules. Do not create a generic `IRepository<T>`.
- A module must not use another module's repository or entities directly; call that module's service.
- No EF navigation properties across modules; reference other modules' data by id only.
- Put code in `Common/` only when it is used by 2+ modules.

## 3. Database & performance

Checklist before coding:

- [ ] No queries inside loops; batch instead (`Where(x => ids.Contains(x.Id))`, `AddRange`, `ExecuteUpdateAsync`)
- [ ] Avoid N+1: project with `Select` into DTOs; use `Include` only when the full entity graph must be modified
- [ ] Several collection `Include`s → `AsSplitQuery()`
- [ ] Read-only queries use `AsNoTracking()`
- [ ] Every list endpoint is paginated (default 20, max 100)
- [ ] Columns used for filtering/sorting/joins have indexes (added in a migration)
- [ ] One transaction per business operation, kept short: validate, load and compute **before** opening it
- [ ] **Never** call external services (payment, email, AI, HTTP) inside a DB transaction
- [ ] Use the default isolation level (`READ COMMITTED`); do not enable `EnableRetryOnFailure` globally
- [ ] Where `Workflow.md` §11.3 says so, retry the whole transaction on `40P01` / `40001` (max 3, short random delay)
- [ ] After a DB error inside a transaction (e.g. unique violation), roll back before running any other query
- [ ] Time comes from `TimeProvider`, never from the database's `now()`
- [ ] Cache only read-heavy, non-user-specific data (catalog, categories); set an expiry and invalidate on write
- [ ] Never use cached stock to decide whether a reservation succeeds
- [ ] For new complex queries, check the generated SQL in the logs

## 4. Inventory & orders (project-critical)

- Stock changes MUST use an atomic conditional update (`ExecuteUpdateAsync` with the condition in `WHERE`).
  Never read → check in C# → save.
- `Available = OnHand - Reserved`. Never store `Available` as a column.
- Every stock change writes a stock-ledger row in the same transaction. Ledger rows are append-only (no update/delete).
- Reservations have an expiry and are released by a background job.
- Order creation and payment callbacks MUST be idempotent (idempotency key + unique constraint).
- Prices and totals are computed on the server and copied into order lines at checkout.
  Never trust prices or totals sent by the client.
- Money uses `decimal` (DB: `numeric(18,2)`). Never `float` or `double`.
- Order status changes go through one method that validates allowed transitions.
- Any change in this section requires a concurrency test (see section 9).

## 5. Security

- Validate every API input with **FluentValidation** on the backend.
  Zod is for frontend forms only — client-side validation is UX, not security.
- Trim/normalize strings and enforce max lengths. Encode on output, not on input
  (React encodes by default; never use `dangerouslySetInnerHTML` with user data).
- Never return EF entities from APIs and never bind request bodies to entities. Use request/response DTOs.
- Every endpoint has `[Authorize]` with a role/policy, or an explicit `[AllowAnonymous]`.
- Check ownership on every resource access (`order.CustomerId == currentUserId`); return 404 if not owned (prevents IDOR).
- The current user id always comes from the authenticated principal, never from the body or query string.
- SQL: use LINQ, or `FromSql` / `ExecuteSql` with interpolated parameters.
  `FromSqlRaw` / `ExecuteSqlRaw` with string concatenation is forbidden.
- Authentication: ASP.NET Core Identity with HttpOnly, Secure, `SameSite=Strict` cookies.
  If JWT is used: validate signature, issuer, audience, lifetime and allowed algorithms — never only decode.
  Never store tokens in `localStorage`.
- API endpoints return `401` / `403`, never a redirect to a login page.
- CSRF: every state-changing request requires the `X-CSRF: 1` header (except the signed webhook).
- Data Protection keys are persisted in the database, never only in the container's file system.
- Behind the reverse proxy, use Forwarded Headers (trust only the proxy) for client IP and scheme.
- Webhooks: verify HMAC signature with a constant-time comparison and reject old timestamps (replay protection).
- Rate limit login, register, checkout and AI endpoints.
- No secrets in code or committed `appsettings*.json`; use user-secrets or environment variables.
- Errors return `ProblemDetails`. Never expose stack traces or SQL in production responses.
- CORS: explicit origins only, never `AllowAnyOrigin`.

## 6. Logging

- Structured logging with message templates: `logger.LogInformation("Order {OrderId} created", orderId)`.
  No string interpolation in log messages.
- Never log passwords, tokens, cookies, or payment data.
- Mask personal data: email `ng***@gmail.com`, phone `090****123`.
- Never log full request/response bodies.
- Expected business failures (out of stock, invalid input) are not logged as `Error`.
- Every log line and every ProblemDetails response carries the request's trace id.

## 7. C# code style

- Nullable reference types enabled, warnings as errors. Do not use `!` to silence a warning without a comment.
- Async all the way: pass `CancellationToken`; no `.Result`, `.Wait()` or `async void`.
- Use `TimeProvider` instead of `DateTime.Now`. Store all times in UTC.
- Expected business outcomes (out of stock, invalid status) return a result type, not an exception.
- Constructor injection only. `DbContext` is scoped; never inject a scoped service into a singleton.
  Background services create a scope per unit of work (`IServiceScopeFactory`).
- Configuration uses options classes with validation and `ValidateOnStart()`.
- Every outgoing HTTP call has a timeout (use `HttpClientFactory` + resilience handler).
- Run `dotnet format` before finishing.
- No new NuGet/npm package without stating why. Do not add MediatR, AutoMapper or MassTransit.

## 8. Frontend (React + TypeScript)

- TypeScript `strict`; no `any`.
- API types are generated from the backend OpenAPI document; do not hand-write duplicate types.
- Zod for form validation.
- Handle loading, empty and error states, including `409` (out of stock) and `429` (rate limited).
- Disable the submit button while a request is pending; send an idempotency key on checkout.

## 9. Testing

- Test **behavior through public seams** agreed in the spec (HTTP endpoints, module service interfaces),
  not private methods or internal collaborators. Do not verify by reading the database directly when the API
  can show the result — except for invariants only the database can show (ledger sums, `Reserved`).
- Mock only what crosses the system boundary (time via `FakeTimeProvider`, external HTTP). Use real PostgreSQL.
- New business logic → unit tests.
- New endpoint → integration test (Testcontainers + PostgreSQL), including one unauthorized/forbidden case.
- Changes to stock, orders or payments → concurrency test
  (e.g. N parallel requests for 1 item → exactly 1 success, stock never negative).
- Test names: `Method_Scenario_ExpectedResult`.
- Tests are deterministic: use `FakeTimeProvider`, no real external network calls.

## 10. Migrations

- Every schema change goes through an EF Core migration with a descriptive name.
- Review the generated migration. Call out any destructive change (dropping a column/table) before applying it.
- Enforce business invariants with unique constraints/indexes (SKU code, idempotency key).
- Migrations never run at application startup; they run as a migration bundle during deployment.
- Migrations must be backward compatible with the previous app version (add nullable first, drop later),
  so a rollback to the previous image still works.

## 11. AI assistant features

- AI tools are read-only. Any write action (add to cart, place order) requires user confirmation in the UI.
- Tools get the user id from the auth context, never from the conversation.
- Product links, prices and stock status come from backend data, never generated by the model.
- Treat product text and user messages as untrusted input (prompt injection).

## 12. Self-review report (required at the end of every coding task)

```
### Self-review
- Issue: #...  | Task: W?-??
- Files changed: ...
- Structure:   ✅ / ❌ (reason)
- DB & perf:   ✅ / ❌ (reason)
- Inventory:   ✅ / ❌ / N/A
- Security:    ✅ / ❌ (reason)
- Logging:     ✅ / ❌ / N/A
- Tests:       added/updated: ...
- Build: pass/fail | Tests: X passed, Y failed
- code-review: standards findings fixed X/Y | spec findings fixed X/Y | open: ...
- Rule exceptions / trade-offs: ... (or "None")
- Follow-ups: ...
```
