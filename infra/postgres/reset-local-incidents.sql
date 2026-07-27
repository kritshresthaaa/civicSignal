-- Clears local operational incident records while preserving identity users,
-- imported historical complaints, and data import job history.
BEGIN;

CREATE TEMP TABLE target_incidents ON COMMIT DROP AS
SELECT id
FROM incidents;

DELETE FROM duplicate_candidates
WHERE incident_id IN (SELECT id FROM target_incidents)
   OR candidate_incident_id IN (SELECT id FROM target_incidents);

DELETE FROM incidents
WHERE id IN (SELECT id FROM target_incidents);

COMMIT;
