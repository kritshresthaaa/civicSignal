# 0001 Use Clean Architecture

## Status

Accepted

## Decision

Use Clean Architecture for project boundaries. The Application layer exposes focused services and interfaces.

## Reason

The project needs clear separation between domain rules, API endpoints, infrastructure integrations, and AI/background processing. A service-based Application layer is easier to understand at this project size and can still grow into more specialized use cases later.
