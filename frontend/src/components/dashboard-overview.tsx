"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { AlertTriangle, Bot, Clock3, Layers, RefreshCw, RadioTower, Route, ShieldCheck, TrendingUp } from "lucide-react";
import {
  CivicApiError,
  getDuplicateCandidates,
  getLatestPrediction,
  searchIncidents,
  type DuplicateCandidateDto,
  type IncidentDto,
  type TriagePredictionDto,
} from "@/lib/civic-api";
import { statusLabel } from "@/lib/civic-analysis";
import type { IncidentStatus, Severity } from "@/lib/civic-types";
import { MetricCard, PageHeader, Panel, ScoreBar, SegmentedControl, SeverityBadge, StatusBadge } from "@/components/ui-kit";

const dashboardTabs = ["Live Queue", "AI Signals", "Agency Load"] as const;
type DashboardState = "loading" | "live" | "empty" | "error";

type DashboardIncident = {
  agency: string;
  aiSummary: string;
  age: string;
  category: string;
  confidence: number;
  createdAt: string;
  description: string;
  duplicateCount: number;
  evidenceCount: number;
  id: string;
  location: string;
  severity: Severity;
  slaRisk: number;
  status: IncidentStatus;
  title: string;
};

type ActivityItem = {
  label: string;
  time: string;
  tone: "critical" | "review" | "calm" | "default";
};

export function DashboardOverview() {
  const [tab, setTab] = useState<(typeof dashboardTabs)[number]>("Live Queue");
  const [rows, setRows] = useState<DashboardIncident[]>([]);
  const [state, setState] = useState<DashboardState>("loading");
  const [message, setMessage] = useState("Loading live operations metrics from the CivicSignal API...");
  const [selectedZone, setSelectedZone] = useState(1);
  const [activePulse, setActivePulse] = useState(0);

  const loadDashboard = useCallback(async () => {
    setState("loading");
    setMessage("Loading live operations metrics from the CivicSignal API...");

    try {
      const incidents = await searchIncidents({ pageSize: 100 });
      const mappedRows = await Promise.all(incidents.map(loadDashboardIncident));

      setRows(mappedRows);
      setState(mappedRows.length ? "live" : "empty");
      setMessage(
        mappedRows.length
          ? `${mappedRows.length} live incidents loaded from backend API records.`
          : "Backend API is live, but there are no incidents yet. Submit a citizen report to populate this dashboard.",
      );
    } catch (error) {
      setRows([]);
      setState("error");
      setMessage(error instanceof CivicApiError ? error.message : "Could not load dashboard metrics from the backend API.");
    }
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadDashboard();
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadDashboard]);

  const openCount = rows.filter((incident) => incident.status !== "Approved").length;
  const reviewCount = rows.filter((incident) => incident.status === "HumanReviewRequired").length;
  const duplicateCount = rows.reduce((total, incident) => total + incident.duplicateCount, 0);
  const averageConfidence = rows.length
    ? rows.reduce((total, incident) => total + incident.confidence, 0) / rows.length
    : 0;
  const zones = useMemo(() => buildCityZones(rows), [rows]);
  const activeSelectedZone = zones.some((item) => item.zone === selectedZone)
    ? selectedZone
    : (zones[0]?.zone ?? selectedZone);
  const zone = zones.find((item) => item.zone === activeSelectedZone) ?? zones[0];
  const activityFeed = useMemo(() => buildActivityFeed(rows, state), [rows, state]);
  const categoryRows = useMemo(() => buildCategoryRows(rows), [rows]);
  const agencyRows = useMemo(() => buildAgencyRows(rows), [rows]);
  const decisionSignals = useMemo(() => buildDecisionSignals(rows), [rows]);

  useEffect(() => {
    const timer = window.setInterval(() => {
      setActivePulse((current) => (current + 1) % activityFeed.length);
    }, 3200);

    return () => window.clearInterval(timer);
  }, [activityFeed.length]);

  return (
    <div className="space-y-6">
      <PageHeader
        actions={
          <>
            <button
              className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface px-3 text-sm font-semibold text-civic-primary transition hover:bg-civic-soft disabled:cursor-not-allowed disabled:opacity-60"
              disabled={state === "loading"}
              onClick={() => void loadDashboard()}
              type="button"
            >
              <RefreshCw className={`h-4 w-4 ${state === "loading" ? "animate-spin" : ""}`} aria-hidden="true" />
              Refresh
            </button>
            <SegmentedControl onChange={setTab} options={dashboardTabs} value={tab} />
          </>
        }
        description="Backend-driven metrics for report intake, AI triage, duplicate detection, human review, and agency dispatch."
        eyebrow="Operations"
        title="Dashboard"
      />

      <div className={`rounded-lg border p-4 text-sm font-semibold ${stateTone(state)}`}>
        {message}
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard icon={<Layers className="h-5 w-5" />} label="Open incidents" trend="From live API records" value={String(openCount)} />
        <MetricCard icon={<ShieldCheck className="h-5 w-5" />} label="Needs review" tone="review" trend="Awaiting human decision" value={String(reviewCount)} />
        <MetricCard icon={<Route className="h-5 w-5" />} label="Duplicate links" tone="calm" trend="Backend candidates found" value={String(duplicateCount)} />
        <MetricCard icon={<Bot className="h-5 w-5" />} label="AI confidence" tone="alert" trend="Average latest prediction" value={`${Math.round(averageConfidence * 100)}%`} />
      </div>

      {tab === "Live Queue" ? (
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1.1fr)_minmax(360px,0.9fr)]">
          <Panel title="Priority Queue" description="Sorted from live incidents by SLA risk and operational severity.">
            {rows.length ? (
              <div className="grid gap-3">
                {rows
                  .slice()
                  .sort((left, right) => right.slaRisk - left.slaRisk)
                  .map((incident) => (
                    <div className="group rounded-md border border-civic-border bg-civic-raised p-4 transition hover:-translate-y-0.5 hover:border-civic-primary hover:bg-civic-soft" key={incident.id}>
                      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                        <div>
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="break-all font-semibold text-civic-heading">{shortId(incident.id)}</span>
                            <SeverityBadge severity={incident.severity} />
                            <StatusBadge status={incident.status} />
                          </div>
                          <h3 className="mt-2 text-lg font-semibold text-civic-heading">{incident.title}</h3>
                          <p className="mt-1 text-sm leading-6 text-civic-muted">{incident.aiSummary}</p>
                        </div>
                        <div className="min-w-40">
                          <ScoreBar label="SLA risk" score={incident.slaRisk} />
                        </div>
                      </div>
                    </div>
                  ))}
              </div>
            ) : (
              <EmptyPanel label={state === "loading" ? "Loading live queue..." : "No live incidents are available for the priority queue."} />
            )}
          </Panel>

          <div className="grid gap-6">
            <Panel title="City Pulse" description={zone ? `Zone ${zone.zone}: ${zone.openReports} open reports and ${zone.pressure}% pressure.` : "Zone pressure appears after live incidents load."}>
              {zones.length ? (
                <>
                  <div className="grid grid-cols-6 gap-2">
                    {zones.map((item, index) => (
                      <button
                        aria-label={`Inspect zone ${item.zone}`}
                        aria-pressed={activeSelectedZone === item.zone}
                        className={`h-12 rounded-md text-xs font-semibold transition hover:scale-[1.04] focus:ring-2 focus:ring-civic-primary/30 ${
                          activeSelectedZone === item.zone ? "ring-2 ring-civic-primary ring-offset-2" : ""
                        } ${zoneColor(item.pressure)}`}
                        key={item.zone}
                        onClick={() => setSelectedZone(item.zone)}
                        style={{ animationDelay: `${index * 18}ms` }}
                        type="button"
                      >
                        {item.zone}
                      </button>
                    ))}
                  </div>
                  <div className="mt-5 rounded-md border border-civic-border bg-civic-raised p-4">
                    <ScoreBar label="Selected zone pressure" score={zone?.pressure ?? 0} />
                  </div>
                </>
              ) : (
                <EmptyPanel label="Zone pressure needs at least one live incident with coordinates." />
              )}
            </Panel>

            <Panel
              action={<RadioTower className="h-5 w-5 text-civic-primary" aria-hidden="true" />}
              title="Live Activity"
              description={activityFeed[activePulse]?.label ?? "Waiting for activity."}
            >
              <div className="grid gap-2">
                {activityFeed.map((event, index) => (
                  <button
                    className={`rounded-md border p-3 text-left transition ${
                      activePulse === index
                        ? "border-civic-primary bg-civic-soft"
                        : "border-civic-border bg-civic-raised hover:border-civic-border-strong"
                    }`}
                    key={`${event.label}-${event.time}`}
                    onClick={() => setActivePulse(index)}
                    type="button"
                  >
                    <div className="flex items-center justify-between gap-3">
                      <span className="text-sm font-semibold text-civic-heading">{event.label}</span>
                      <span className={`rounded-md px-2 py-1 text-xs font-semibold ${activityTone(event.tone)}`}>{event.time}</span>
                    </div>
                  </button>
                ))}
              </div>
            </Panel>
          </div>
        </div>
      ) : null}

      {tab === "AI Signals" ? (
        <div className="grid gap-6 xl:grid-cols-2">
          <Panel title="Category Mix" description="Current triage distribution from live backend records.">
            {categoryRows.length ? (
              <div className="grid gap-4">
                {categoryRows.map((item) => (
                  <div key={item.label}>
                    <div className="mb-2 flex justify-between text-sm">
                      <span className="font-medium text-civic-ink">{item.label}</span>
                      <span className="text-civic-muted">{item.count} cases</span>
                    </div>
                    <div className="h-3 rounded-full bg-civic-border">
                      <div className="h-3 rounded-full bg-civic-primary" style={{ width: `${item.width}%` }} />
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <EmptyPanel label="Category distribution appears after live incidents are created." />
            )}
          </Panel>

          <Panel title="Decision Quality" description="Signals derived from latest backend predictions and duplicate candidates.">
            <div className="grid gap-3">
              {decisionSignals.map((item) => (
                <div className="flex items-center justify-between rounded-md border border-civic-border bg-civic-raised p-4" key={item.label}>
                  <div className="flex items-center gap-3">
                    <span className="rounded-md bg-civic-soft p-2 text-civic-primary">
                      <item.icon className="h-4 w-4" aria-hidden="true" />
                    </span>
                    <span className="font-medium text-civic-ink">{item.label}</span>
                  </div>
                  <span className="text-lg font-semibold text-civic-heading">{item.value}</span>
                </div>
              ))}
            </div>
          </Panel>
        </div>
      ) : null}

      {tab === "Agency Load" ? (
        <Panel title="Agency Capacity" description="Queue pressure by receiving agency from live incident records.">
          {agencyRows.length ? (
            <div className="grid gap-4">
              {agencyRows.map((agency) => (
                <div className="rounded-md border border-civic-border bg-civic-raised p-4" key={agency.agency}>
                  <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                    <div>
                      <h3 className="text-lg font-semibold text-civic-heading">{agency.agency}</h3>
                      <p className="text-sm text-civic-muted">
                        {agency.open} open of {agency.capacity} planned crew slots
                      </p>
                    </div>
                    <div className="min-w-56">
                      <ScoreBar label="SLA risk" score={agency.slaRisk} />
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <EmptyPanel label="Agency load appears after incidents are routed or predicted." />
          )}
        </Panel>
      ) : null}
    </div>
  );
}

async function loadDashboardIncident(incident: IncidentDto): Promise<DashboardIncident> {
  const [predictionResult, duplicateResult] = await Promise.allSettled([
    getLatestPrediction(incident.id),
    getDuplicateCandidates(incident.id),
  ]);
  const prediction = predictionResult.status === "fulfilled" ? predictionResult.value : null;
  const duplicates = duplicateResult.status === "fulfilled" ? duplicateResult.value : [];

  return mapDashboardIncident(incident, prediction, duplicates);
}

function mapDashboardIncident(
  incident: IncidentDto,
  prediction: TriagePredictionDto | null,
  duplicates: DuplicateCandidateDto[],
): DashboardIncident {
  const category = incident.correctedCategory ?? prediction?.category ?? inferCategory(incident.description);
  const agency = incident.correctedAgencyCode ?? prediction?.suggestedAgencyCode ?? inferAgency(category);
  const severity = normalizeSeverity(incident.correctedSeverity ?? prediction?.severity ?? inferSeverity(incident.description));
  const status = normalizeStatus(incident.status);
  const confidence = prediction?.confidence ?? (incident.acceptedPrediction ? 0.86 : 0.58);
  const duplicateCount = duplicates.length + (incident.duplicateOfIncidentId ? 1 : 0);

  return {
    agency,
    aiSummary: prediction?.summary ?? `${statusLabel(category)} report loaded from backend persistence with geospatial coordinates.`,
    age: formatAge(incident.createdAt),
    category,
    confidence,
    createdAt: incident.createdAt,
    description: incident.description,
    duplicateCount,
    evidenceCount: prediction?.evidence.length ?? 0,
    id: incident.id,
    location: `${incident.latitude.toFixed(5)}, ${incident.longitude.toFixed(5)}`,
    severity,
    slaRisk: calculateSlaRisk(status, severity, confidence, duplicateCount),
    status,
    title: `${statusLabel(category)} report`,
  };
}

function buildCityZones(rows: DashboardIncident[]) {
  const grouped = new Map<number, DashboardIncident[]>();

  for (const row of rows) {
    const zone = estimateZone(row.location);
    grouped.set(zone, [...(grouped.get(zone) ?? []), row]);
  }

  return Array.from(grouped.entries())
    .map(([zone, incidents]) => ({
      openReports: incidents.filter((incident) => incident.status !== "Approved").length,
      pressure: Math.min(100, Math.round(incidents.reduce((total, incident) => total + incident.slaRisk, 0) / Math.max(incidents.length, 1))),
      zone,
    }))
    .sort((left, right) => left.zone - right.zone);
}

function buildActivityFeed(rows: DashboardIncident[], state: DashboardState): ActivityItem[] {
  if (rows.length === 0) {
    return [
      {
        label: state === "loading" ? "Loading live backend activity" : "No live incident activity yet",
        time: state === "loading" ? "Now" : "Idle",
        tone: state === "error" ? "critical" : "default",
      },
    ];
  }

  return rows
    .slice()
    .sort((left, right) => Date.parse(right.createdAt) - Date.parse(left.createdAt))
    .slice(0, 5)
    .map((incident) => ({
      label: `${shortId(incident.id)} ${statusLabel(incident.status).toLowerCase()} for ${incident.agency}`,
      time: incident.age,
      tone: incident.severity === "Critical" ? "critical" : incident.status === "HumanReviewRequired" ? "review" : "calm",
    }));
}

function buildCategoryRows(rows: DashboardIncident[]) {
  const counts = countBy(rows.map((row) => row.category));
  const max = Math.max(...counts.map((item) => item.count), 1);

  return counts.map((item) => ({
    ...item,
    width: Math.max(8, Math.round((item.count / max) * 100)),
  }));
}

function buildAgencyRows(rows: DashboardIncident[]) {
  const grouped = countBy(rows.map((row) => row.agency));
  const riskByAgency = new Map<string, number>();

  for (const agency of grouped) {
    const agencyRows = rows.filter((row) => row.agency === agency.label);
    riskByAgency.set(
      agency.label,
      Math.round(agencyRows.reduce((total, row) => total + row.slaRisk, 0) / Math.max(agencyRows.length, 1)),
    );
  }

  return grouped.map((item) => ({
    agency: item.label,
    capacity: Math.max(4, Math.ceil(item.count * 1.4)),
    open: item.count,
    slaRisk: riskByAgency.get(item.label) ?? 0,
  }));
}

function buildDecisionSignals(rows: DashboardIncident[]) {
  const evidenceBacked = rows.length
    ? Math.round((rows.filter((row) => row.evidenceCount > 0).length / rows.length) * 100)
    : 0;
  const autoRouteEligible = rows.length
    ? Math.round((rows.filter((row) => row.confidence >= 0.8 && row.status !== "HumanReviewRequired").length / rows.length) * 100)
    : 0;
  const duplicates = rows.reduce((total, row) => total + row.duplicateCount, 0);
  const averageAgeMinutes = rows.length
    ? Math.round(rows.reduce((total, row) => total + ageInMinutes(row.createdAt), 0) / rows.length)
    : 0;

  return [
    { label: "Evidence-backed predictions", value: `${evidenceBacked}%`, icon: Bot },
    { label: "Auto-route eligibility", value: `${autoRouteEligible}%`, icon: TrendingUp },
    { label: "Potential duplicate reviews", value: String(duplicates), icon: AlertTriangle },
    { label: "Average response age", value: formatMinutes(averageAgeMinutes), icon: Clock3 },
  ];
}

function countBy(values: string[]) {
  const counts = new Map<string, number>();

  for (const value of values) {
    counts.set(value, (counts.get(value) ?? 0) + 1);
  }

  return Array.from(counts.entries())
    .map(([label, count]) => ({ count, label }))
    .sort((left, right) => right.count - left.count || left.label.localeCompare(right.label));
}

function stateTone(state: DashboardState) {
  if (state === "error") {
    return "border-status-critical bg-status-critical/10 text-status-critical-text";
  }

  if (state === "live") {
    return "border-status-approved bg-status-approved/10 text-status-approved-text";
  }

  return "border-civic-border bg-civic-surface text-civic-muted";
}

function activityTone(tone: ActivityItem["tone"]) {
  if (tone === "critical") {
    return "bg-status-critical text-status-critical-text";
  }

  if (tone === "review") {
    return "bg-status-review text-status-review-text";
  }

  if (tone === "calm") {
    return "bg-status-approved text-status-approved-text";
  }

  return "bg-status-submitted text-status-submitted-text";
}

function zoneColor(pressure: number) {
  if (pressure > 78) {
    return "bg-status-critical text-status-critical-text";
  }

  if (pressure > 52) {
    return "bg-status-review text-status-review-text";
  }

  if (pressure > 28) {
    return "bg-status-triaged text-status-triaged-text";
  }

  return "bg-status-approved text-status-approved-text";
}

function normalizeStatus(status: string): IncidentStatus {
  if (status === "Triaged" || status === "HumanReviewRequired" || status === "Approved" || status === "Dispatched") {
    return status;
  }

  return "Submitted";
}

function normalizeSeverity(severity: string): Severity {
  if (severity === "Low" || severity === "Medium" || severity === "High" || severity === "Critical") {
    return severity;
  }

  return "Medium";
}

function inferCategory(description: string) {
  const lower = description.toLowerCase();

  if (lower.includes("drain") || lower.includes("water") || lower.includes("flood")) {
    return "Flooding";
  }

  if (lower.includes("light") || lower.includes("signal")) {
    return "Streetlight";
  }

  if (lower.includes("trash") || lower.includes("dump")) {
    return "Sanitation";
  }

  if (lower.includes("graffiti")) {
    return "Graffiti";
  }

  if (lower.includes("tree") || lower.includes("branch")) {
    return "TreeHazard";
  }

  return "RoadDamage";
}

function inferAgency(category: string) {
  if (category === "RoadDamage" || category === "Streetlight" || category === "TrafficSignal") {
    return "DOT";
  }

  if (category === "TreeHazard") {
    return "PARKS";
  }

  if (category === "Sanitation" || category === "Graffiti") {
    return "DSNY";
  }

  return "DPW";
}

function inferSeverity(description: string) {
  const lower = description.toLowerCase();

  if (lower.includes("danger") || lower.includes("critical") || lower.includes("blocked") || lower.includes("swerving")) {
    return "High";
  }

  if (lower.includes("small") || lower.includes("minor")) {
    return "Low";
  }

  return "Medium";
}

function calculateSlaRisk(status: IncidentStatus, severity: Severity, confidence: number, duplicateCount: number) {
  const severityWeight = severity === "Critical" ? 92 : severity === "High" ? 74 : severity === "Medium" ? 52 : 28;
  const statusWeight = status === "HumanReviewRequired" ? 14 : status === "Submitted" ? 10 : status === "Dispatched" ? 8 : 0;
  const confidencePenalty = Math.max(0, (0.8 - confidence) * 24);
  const duplicateWeight = Math.min(12, duplicateCount * 4);

  return Math.min(99, Math.round(severityWeight + statusWeight + confidencePenalty + duplicateWeight));
}

function estimateZone(location: string) {
  const [latitudeRaw, longitudeRaw] = location.split(",").map((part) => Number(part.trim()));
  const latitude = Number.isFinite(latitudeRaw) ? latitudeRaw : 40.7128;
  const longitude = Number.isFinite(longitudeRaw) ? longitudeRaw : -74.006;
  const normalized = Math.abs(Math.round((latitude * 1000 + longitude * 1000) * 10));

  return (normalized % 30) + 1;
}

function shortId(value: string) {
  return value.length > 16 ? `${value.slice(0, 8)}...${value.slice(-6)}` : value;
}

function formatAge(value: string) {
  const minutes = ageInMinutes(value);

  return formatMinutes(minutes);
}

function ageInMinutes(value: string) {
  const timestamp = Date.parse(value);

  if (!Number.isFinite(timestamp)) {
    return 0;
  }

  return Math.max(0, Math.round((Date.now() - timestamp) / 60_000));
}

function formatMinutes(minutes: number) {
  if (minutes < 60) {
    return `${minutes}m`;
  }

  const hours = Math.floor(minutes / 60);
  const remainder = minutes % 60;

  return remainder ? `${hours}h ${remainder}m` : `${hours}h`;
}

function EmptyPanel({ label }: { label: string }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm font-semibold text-civic-muted">
      {label}
    </div>
  );
}
