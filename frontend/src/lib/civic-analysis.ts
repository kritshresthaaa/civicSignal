import type { DuplicateCandidate, Severity } from "@/lib/civic-types";

export type DraftAnalysis = {
  agency: string;
  category: string;
  duplicates: DuplicateCandidate[];
  readiness: number;
  severity: Severity;
  summary: string;
};

export function analyzeDraft(description: string, latitude: string, longitude: string, hasMedia: boolean): DraftAnalysis {
  const lower = description.toLowerCase();
  const category =
    lower.includes("drain") || lower.includes("water")
      ? "Drainage"
      : lower.includes("sidewalk")
        ? "Sidewalk"
        : lower.includes("signal") || lower.includes("light")
          ? "TrafficSignal"
          : "RoadDamage";
  const agency = category === "RoadDamage" || category === "TrafficSignal" ? "DOT" : "DPW";
  const severity: Severity =
    lower.includes("swerving") || lower.includes("blocked")
      ? "High"
      : lower.includes("dark") || lower.includes("flood") || lower.includes("hazard")
        ? "Critical"
        : "Medium";
  const hasLocation = Boolean(latitude.trim() && longitude.trim());
  const readiness = Math.min(
    100,
    25 + Math.min(description.length, 140) / 2 + (hasLocation ? 20 : 0) + (hasMedia ? 12 : 0),
  );
  const summary = `${category} report routed to ${agency} with ${severity.toLowerCase()} urgency. ${
    hasLocation ? "Coordinates are available for geospatial duplicate search." : "Location still needs confirmation."
  }`;

  return {
    agency,
    category,
    duplicates: [],
    readiness,
    severity,
    summary,
  };
}

export function statusLabel(status: string) {
  return status.replace(/([a-z])([A-Z])/g, "$1 $2");
}
