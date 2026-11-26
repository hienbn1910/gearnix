---
status: accepted
---

# CSRF protection: SameSite=Strict plus a required custom header

Cookie authentication (ADR 0004) means the browser attaches the auth cookie to every request it sends to the API. Another website could therefore try to make a signed-in user's browser change data (cross-site request forgery). We use two layers:

1. The cookie is `SameSite=Strict`, so browsers do not send it on cross-site requests at all.
2. Every `POST`, `PUT`, `PATCH` and `DELETE` under `/api` must carry `X-CSRF: 1`. Otherwise the request is rejected with `403 csrf_header_missing`.

A cross-site page cannot add a custom header without a CORS preflight, and the API grants no cross-origin access, so the second layer holds even if a browser mishandles SameSite.

## Considered Options

- **SameSite only.** It is simple, but it depends on every browser applying SameSite correctly, and it does not cover "same-site but different origin" cases (for example another subdomain). A second, independent layer costs one middleware.
- **Synchronizer or double-submit tokens.** These work, but they need a token endpoint, storage and per-form plumbing in both frontends. A constant custom header gives the same guarantee for a JSON API that only its own single-page apps call.
- **Custom header (chosen).** The check is one middleware, and the frontends add the header once in their HTTP client.

## Consequences

- **Login and register are included.** This blocks login CSRF, where an attacker signs the victim into the attacker's account.
- **The payment webhook is exempt** (`/api/payments/webhook`). It is a server-to-server call that carries no cookie and is authenticated by its HMAC signature (`Workflow.md` §7.9).
- **The API must never allow cross-origin access.** Allowing any origin together with the `X-CSRF` header would disable this protection. `Rule.md` §5 already forbids `AllowAnyOrigin`.
- **All API clients send the header.** That includes integration tests: their client helper sends it by default, and dedicated tests check what happens without it.
