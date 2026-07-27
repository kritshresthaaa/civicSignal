# 0003 Track Incident Processing Steps

## Status

Accepted

## Decision

Track processing progress as child `ProcessingStep` records under each incident. Each step has a name, status, timestamps, and optional error message.

## Reason

CivicSignal needs visibility into asynchronous AI and operations work without adding a separate workflow engine yet. A child-table model keeps the backend simple, supports the Worker project later, and gives the API enough state to show whether an incident was geocoded, analyzed, duplicate-checked, triaged, or failed.
