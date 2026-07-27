# 0019 Add OSM Nominatim Geocoding

## Status

Accepted

## Context

The requirements call for OpenStreetMap/Nominatim and a citizen location workflow. Browser geolocation and manual coordinates are useful, but citizens often think in addresses and intersections.

## Decision

Add `IGeocodingService` in `Application`. `Infrastructure` provides a Nominatim adapter that is disabled by default and cached through `IApplicationCache`. The API exposes public, rate-limited endpoints for address search and reverse geocoding:

- `GET /api/geocoding/search`
- `GET /api/geocoding/reverse`

The citizen PWA calls these API endpoints only.

## Consequences

The frontend gets address-to-coordinate and coordinate-to-address functionality without direct access to external providers. Production can replace Nominatim with another geocoder behind the same interface.
