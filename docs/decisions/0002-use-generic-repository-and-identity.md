# 0002 Use Generic Repository and Identity Foundation

## Status

Accepted

## Decision

Use `IGenericRepository<TEntity>` for shared persistence operations and specialized repositories for domain-specific queries. Use ASP.NET Core Identity for access management persistence and role support.

## Reason

Generic repositories reduce repeated CRUD code, while specialized repositories keep important query intent visible. CivicSignal needs access management for reporters, reviewers, operators, and administrators, so Identity gives the backend a standard user, role, claim, and lockout model without building security primitives from scratch.
