# Load Testing

CivicSignal includes k6 scripts for repeatable local performance checks before demos and deployments.

## Prerequisites

- k6 installed locally
- Full backend stack running
- Seeded operator account available when running staff tests

```bash
docker compose up --build
```

## Public Report Flow

This script checks API health, submits public incidents, loads tracking details, checks public status, and refreshes the public feed.

```bash
k6 run load-tests/k6/public-report-flow.js
```

Useful overrides:

```bash
API_BASE_URL=http://localhost:5020 VUS=10 DURATION=2m k6 run load-tests/k6/public-report-flow.js
```

## Staff Read Flow

This script logs in as an operator and exercises dashboard-style reads for incidents, data imports, evaluation, forecasts, and system configuration.

```bash
k6 run load-tests/k6/staff-read-flow.js
```

Useful overrides:

```bash
API_BASE_URL=http://localhost:5020 \
OPERATOR_EMAIL=operator@civicsignal.local \
OPERATOR_PASSWORD=Operator123456! \
VUS=5 \
DURATION=2m \
k6 run load-tests/k6/staff-read-flow.js
```

## Targets

Current starter thresholds:

- Public flow: less than 2% failed requests, p95 below 900 ms
- Staff flow: less than 3% failed requests, p95 below 1200 ms

Publish final demo results in `evaluation/reports/` after running against the deployment target.
