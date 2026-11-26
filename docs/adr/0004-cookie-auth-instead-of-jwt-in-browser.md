---
status: accepted
---

# Cookie authentication instead of JWT in the browser

The storefront and admin are single-page apps served from the same domain as the API, with Caddy proxying `/api` (`Workflow.md` §5.1). We authenticate with an ASP.NET Core Identity cookie: `gearnix.auth`, HttpOnly, Secure, `SameSite=Strict`, 7-day sliding expiration. We do not use JSON Web Tokens kept by JavaScript. Data Protection keys, which encrypt the cookie, are stored in PostgreSQL so that redeploying the container does not sign everyone out.

## Considered Options

- **JWT in `localStorage` or `sessionStorage`.** Any script on the page can read the token, so one XSS bug leaks a long-lived credential that keeps working away from the site. Revoking a JWT before it expires also needs extra machinery, such as a deny-list or short tokens plus refresh tokens. Rejected.
- **JWT in an HttpOnly cookie.** This has the same CSRF exposure as an Identity cookie, but adds token signing, key rotation, refresh flows and claim-staleness problems. With one first-party client on one domain, none of that buys anything.
- **Identity cookie (chosen).** It uses a built-in, well-reviewed mechanism. Scripts cannot read it, and `SameSite=Strict` already stops most cross-site requests. Signing out deletes the cookie, and the security stamp can invalidate sessions when needed.

## Consequences

- **CSRF.** Cookies are sent automatically, so the API needs CSRF protection. See ADR 0006.
- **No redirects.** The cookie handler's default redirects to a login page are replaced by `401`/`403` ProblemDetails, because an API client cannot follow an HTML login redirect.
- **Single domain.** The API and the frontends must share a site (Caddy in production, the Vite proxy in development); cross-site use of the cookie is impossible by design.
- **Startup database access.** Because the keys are in the database, the Data Protection key ring loads them at startup. If PostgreSQL is down, startup only logs an error: the API still starts, `/api/health/live` answers, and the keys are retried on first use.
- **Known Identity limits.**
  - Signing out deletes the cookie in the browser. A copied cookie stays valid until it expires or until the
    security stamp is re-checked (every 30 minutes by default).
  - Parallel wrong-password attempts can under-count failures, because the counter update uses optimistic
    concurrency, so an attacker may get a few more than 5 guesses. Rate limiting (W4-06) bounds this.
- **Not revealing which emails exist.** Sign-in returns the same `401 invalid_credentials` for an unknown email, a wrong password and a locked account. Unknown emails and locked accounts still pay for a password hash, so timing does not reveal which emails exist. Registration necessarily reveals an existing email (`409 email_taken`); that trade-off is accepted for usability, and rate limiting (W4-06) slows down bulk probing.
