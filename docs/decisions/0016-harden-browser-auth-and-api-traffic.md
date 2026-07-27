# 0016 Harden Browser Auth and API Traffic

## Status

Accepted

## Context

CivicSignal supports both browser-based staff sessions and API-style clients. Browser sessions use HttpOnly access and refresh token cookies, while Swagger, mobile clients, and service clients can use `Authorization: Bearer`.

Cookie-backed unsafe requests need CSRF protection because browsers attach cookies automatically. Bearer-token clients should not need CSRF tokens because their credentials are intentionally attached by the caller.

## Decision

Add an anonymous `GET /api/auth/csrf` endpoint that returns an antiforgery request token and stores the antiforgery cookie. The frontend fetch wrapper sends that token in `X-CSRF-TOKEN` for unsafe requests.

The API validates CSRF only when an unsafe `/api` request is using CivicSignal auth cookies and does not include a bearer token. This keeps browser sessions protected without making Swagger/API clients awkward.

Add ASP.NET Core rate limiting with:

- A global API backstop.
- A stricter auth policy for login, register, refresh, logout, and CSRF token requests.
- A public-write policy for citizen submissions, uploads, update requests, notification preferences, and feedback.

Add baseline security headers: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `X-Permitted-Cross-Domain-Policies`, `X-Download-Options`, and `Permissions-Policy`.

## Consequences

The frontend must call the API through the shared `requestJson` helper or include `X-CSRF-TOKEN` manually for unsafe cookie-authenticated requests. Bearer clients continue to work without CSRF headers.
