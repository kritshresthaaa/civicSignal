"use client";

import { useEffect, useMemo, useState } from "react";
import { Activity, BarChart3, BrainCircuit, Building2, Database, GitMerge, Loader2, MapPin, Route, TimerReset, TrendingUp } from "lucide-react";
import {
  getDuplicateCandidates,
  getHistoricalComplaintSummary,
  getIncidentVolumeForecast,
  getLatestPrediction,
  searchIncidents,
  searchHistoricalComplaints,
  type DuplicateCandidateDto,
  type HistoricalComplaintDto,
  type HistoricalComplaintSummaryDto,
  type IncidentDto,
  type IncidentForecastDto,
  type TriagePredictionDto,
} from "@/lib/civic-api";
import { MetricCard, PageHeader, Panel, ScoreBar, SegmentedControl } from "@/components/ui-kit";

const ranges = ["Today", "7 Days", "30 Days"] as const;
const rangeConfig: Record<(typeof ranges)[number], { historyDays: number; horizonDays: number }> = {
  Today: { historyDays: 14, horizonDays: 1 },
  "7 Days": { historyDays: 30, horizonDays: 7 },
  "30 Days": { historyDays: 90, horizonDays: 30 },
};

type CategoryChartRow = {
  count?: number;
  label: string;
  value: number;
  width: number;
};

export function AnalyticsCommand() {
  const [range, setRange] = useState<(typeof ranges)[number]>("7 Days");
  const [forecast, setForecast] = useState<IncidentForecastDto | null>(null);
  const [forecastStatus, setForecastStatus] = useState<"loading" | "ready" | "fallback">("loading");
  const [historicalComplaints, setHistoricalComplaints] = useState<HistoricalComplaintDto[]>([]);
  const [historicalSummary, setHistoricalSummary] = useState<HistoricalComplaintSummaryDto | null>(null);
  const [historicalStatus, setHistoricalStatus] = useState<"loading" | "ready" | "empty" | "fallback">("loading");
  const [liveIncidents, setLiveIncidents] = useState<IncidentDto[]>([]);
  const [liveDuplicates, setLiveDuplicates] = useState<DuplicateCandidateDto[]>([]);
  const [livePredictions, setLivePredictions] = useState<Record<string, TriagePredictionDto>>({});
  const [incidentStatus, setIncidentStatus] = useState<"loading" | "ready" | "empty" | "fallback">("loading");

  const analytics = useMemo(() => {
    const forecastTotal = forecast?.history.reduce((total, point) => total + (point.actualCount ?? 0), 0);
    const total = forecastTotal && forecastTotal > 0 ? forecastTotal : liveIncidents.length;
    const autoRouted = liveIncidents.filter((incident) => incident.status !== "HumanReviewRequired" && incident.status !== "Submitted").length;
    const reviewed = liveIncidents.filter((incident) => incident.status === "HumanReviewRequired" || incident.reviewDecision).length;

    return { total, autoRouted, reviewed, duplicates: liveDuplicates.length };
  }, [forecast, liveDuplicates.length, liveIncidents]);

  useEffect(() => {
    let ignore = false;
    const options = rangeConfig[range];

    getIncidentVolumeForecast(options)
      .then((result) => {
        if (!ignore) {
          setForecast(result);
          setForecastStatus("ready");
        }
      })
      .catch(() => {
        if (!ignore) {
          setForecast(null);
          setForecastStatus("fallback");
        }
      });

    return () => {
      ignore = true;
    };
  }, [range]);

  useEffect(() => {
    let ignore = false;

    searchIncidents({ pageSize: 100 })
      .then(async (incidents) => {
        const [duplicateResults, predictionResults] = await Promise.all([
          Promise.allSettled(incidents.map((incident) => getDuplicateCandidates(incident.id))),
          Promise.allSettled(incidents.map((incident) => getLatestPrediction(incident.id))),
        ]);

        if (ignore) {
          return;
        }

        setLiveIncidents(incidents);
        setLiveDuplicates(
          duplicateResults.flatMap((result) => (result.status === "fulfilled" ? result.value : [])),
        );
        setLivePredictions(
          Object.fromEntries(
            predictionResults.flatMap((result, index) =>
              result.status === "fulfilled" && result.value ? [[incidents[index].id, result.value]] : [],
            ),
          ),
        );
        setIncidentStatus(incidents.length ? "ready" : "empty");
      })
      .catch(() => {
        if (!ignore) {
          setLiveIncidents([]);
          setLiveDuplicates([]);
          setLivePredictions({});
          setIncidentStatus("fallback");
        }
      });

    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    let ignore = false;

    Promise.all([
      searchHistoricalComplaints({ pageSize: 250 }),
      getHistoricalComplaintSummary({ pageSize: 1 }),
    ])
      .then(([complaints, summary]) => {
        if (ignore) {
          return;
        }

        setHistoricalComplaints(complaints);
        setHistoricalSummary(summary);
        setHistoricalStatus(summary.totalCount > 0 || complaints.length > 0 ? "ready" : "empty");
      })
      .catch(() => {
        if (!ignore) {
          setHistoricalComplaints([]);
          setHistoricalSummary(null);
          setHistoricalStatus("fallback");
        }
      });

    return () => {
      ignore = true;
    };
  }, []);

  const handleRangeChange = (nextRange: (typeof ranges)[number]) => {
    if (nextRange !== range) {
      setForecastStatus("loading");
      setRange(nextRange);
    }
  };

  const forecastPoints = forecast?.forecast ?? [];
  const historyPoints = forecast?.history.slice(-12) ?? [];
  const futureVolume = Math.round(forecastPoints.reduce((total, point) => total + point.forecastCount, 0));
  const peakForecast = Math.max(...forecastPoints.map((point) => point.upperBound), ...historyPoints.map((point) => point.actualCount ?? point.forecastCount), 1);
  const modelLabel = forecastStatus === "ready" && forecast ? forecast.modelName : "backend forecast unavailable";
  const livePredictionMap = useMemo(() => new Map(Object.entries(livePredictions)), [livePredictions]);
  const categoryRows = useMemo(() => buildCategoryRows(historicalSummary, liveIncidents, livePredictionMap), [historicalSummary, liveIncidents, livePredictionMap]);
  const agencyPressureRows = useMemo(() => buildAgencyPressureRows(historicalSummary, liveIncidents, livePredictionMap), [historicalSummary, liveIncidents, livePredictionMap]);
  const duplicateRows = useMemo(() => buildDuplicateRows(liveDuplicates), [liveDuplicates]);
  const boroughRows = historicalSummary?.topBoroughs.slice(0, 5) ?? [];
  const historicalTotal = historicalSummary?.totalCount ?? historicalComplaints.length;
  const topHistoricalCategory = historicalSummary?.topCategories[0];
  const topHistoricalBorough = historicalSummary?.topBoroughs[0];

  return (
    <div className="space-y-6">
      <PageHeader
        actions={<SegmentedControl onChange={handleRangeChange} options={ranges} value={range} />}
        description="Operational analytics for intake volume, AI confidence, duplicate detection, and agency response pressure."
        eyebrow="Analytics"
        title="Performance Command"
      />

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard icon={<Activity className="h-5 w-5" />} label="Reports" trend={incidentStatusLabel(incidentStatus)} value={String(analytics.total)} />
        <MetricCard icon={<BrainCircuit className="h-5 w-5" />} label="Auto-routed" trend="Live cases beyond submission/review" value={String(analytics.autoRouted)} />
        <MetricCard icon={<GitMerge className="h-5 w-5" />} label="Duplicate holds" trend="Backend candidates found" value={String(analytics.duplicates)} />
        <MetricCard icon={<TimerReset className="h-5 w-5" />} label="Human review" trend="Reviewer-assisted cases" value={String(analytics.reviewed)} />
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <MetricCard
          icon={<Database className="h-5 w-5" />}
          label="Historical 311"
          trend={historicalStatusLabel(historicalStatus)}
          value={formatCompactNumber(historicalTotal)}
        />
        <MetricCard
          icon={<BarChart3 className="h-5 w-5" />}
          label="Top 311 type"
          tone="calm"
          trend={topHistoricalCategory ? `${formatCompactNumber(topHistoricalCategory.count)} imported records` : "Waiting for public data"}
          value={topHistoricalCategory?.value ?? "None"}
        />
        <MetricCard
          icon={<MapPin className="h-5 w-5" />}
          label="Top borough"
          tone="review"
          trend={topHistoricalBorough ? `${formatCompactNumber(topHistoricalBorough.count)} imported records` : "Waiting for public data"}
          value={topHistoricalBorough?.value ?? "None"}
        />
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(360px,0.9fr)]">
        <Panel
          action={<BarChart3 className="h-6 w-6 text-civic-primary" aria-hidden="true" />}
          title={historicalSummary?.topCategories.length ? "311 Category Distribution" : "Category Distribution"}
          description="Share of imported public complaints, or live incident categories when historical data is not imported."
        >
          {categoryRows.length ? (
            <div className="grid gap-4">
              {categoryRows.map((item) => (
                <div className="grid gap-2 md:grid-cols-[140px_minmax(0,1fr)_48px]" key={item.label}>
                  <span className="text-sm font-medium text-civic-ink">{item.label}</span>
                  <div className="h-4 overflow-hidden rounded-full bg-civic-border">
                    <div className="h-4 rounded-full bg-civic-primary" style={{ width: `${item.width}%` }} />
                  </div>
                  <span className="text-right text-sm font-semibold text-civic-heading">
                    {item.count === undefined ? `${item.value}%` : formatCompactNumber(item.count)}
                  </span>
                </div>
              ))}
            </div>
          ) : (
            <EmptyState label="Category analytics appear after incidents or historical complaints are loaded." />
          )}
        </Panel>

        <Panel
          action={
            <div className="inline-flex items-center gap-2 rounded-md bg-civic-soft px-3 py-2 text-sm font-semibold text-civic-primary">
              {forecastStatus === "loading" ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <TrendingUp className="h-4 w-4" aria-hidden="true" />}
              {forecastStatus === "ready" ? "Backend forecast" : "Forecast preview"}
            </div>
          }
          title="Incident Volume Forecast"
          description={`Projected ${range.toLowerCase()} workload from ${modelLabel}.`}
        >
          <div className="grid gap-5">
            <div className="grid grid-cols-3 gap-3">
              <ForecastStat label="Horizon" value={`${forecastPoints.length}d`} />
              <ForecastStat label="Projected" value={String(futureVolume)} />
              <ForecastStat label="Peak band" value={String(peakForecast)} />
            </div>

            {historyPoints.length || forecastPoints.length ? (
              <div className="rounded-lg border border-civic-border bg-civic-raised p-4">
                <div className="grid min-h-56 grid-cols-[repeat(12,minmax(12px,1fr))_repeat(7,minmax(12px,1fr))] items-end gap-1 md:gap-2">
                  {historyPoints.map((point) => (
                    <ForecastBar
                      key={`history-${point.date}`}
                      label={formatShortDate(point.date)}
                      max={peakForecast}
                      tone="history"
                      value={point.actualCount ?? point.forecastCount}
                    />
                  ))}
                  {forecastPoints.slice(0, 7).map((point) => (
                    <ForecastBar
                      key={`forecast-${point.date}`}
                      label={formatShortDate(point.date)}
                      max={peakForecast}
                      tone="forecast"
                      value={point.forecastCount}
                    />
                  ))}
                </div>
              </div>
            ) : (
              <EmptyState label="Forecast bars appear after the backend forecast endpoint returns data." />
            )}

            <div className="grid gap-2">
              {forecastPoints.slice(0, 4).map((point) => (
                <div className="grid grid-cols-[84px_minmax(0,1fr)_56px] items-center gap-3 text-sm" key={point.date}>
                  <span className="font-medium text-civic-muted">{formatShortDate(point.date)}</span>
                  <div className="h-2 overflow-hidden rounded-full bg-civic-border">
                    <div
                      className="h-full rounded-full bg-civic-primary transition-all duration-500"
                      style={{ width: `${Math.min(100, (point.forecastCount / peakForecast) * 100)}%` }}
                    />
                  </div>
                  <span className="text-right font-semibold text-civic-heading">{Math.round(point.forecastCount)}</span>
                </div>
              ))}
            </div>
          </div>
        </Panel>
      </div>

      <div className="grid gap-6 xl:grid-cols-3">
        <Panel title="Agency 311 Pressure" description="Receiving agency pressure from imported complaints when available.">
          {agencyPressureRows.length ? (
            <div className="grid gap-4">
              {agencyPressureRows.map((agency) => (
                <div className="rounded-md border border-civic-border bg-civic-raised p-4" key={agency.agency}>
                  <div className="mb-3 flex items-center justify-between">
                    <div className="flex items-center gap-2 font-semibold text-civic-heading">
                      <Route className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                      {agency.agency}
                    </div>
                    <span className="text-sm text-civic-muted">
                      {agency.open}/{agency.capacity}
                    </span>
                  </div>
                  <ScoreBar label="SLA risk" score={agency.slaRisk} />
                </div>
              ))}
            </div>
          ) : (
            <EmptyState label="Agency pressure appears after incidents or historical complaints are routed." />
          )}
        </Panel>

        <Panel title="Borough Hotspots" description="Historical complaint concentration by service area.">
          {boroughRows.length ? (
            <div className="grid gap-3">
              {boroughRows.map((borough) => (
                <div className="rounded-md border border-civic-border bg-civic-raised p-4" key={borough.value}>
                  <div className="mb-3 flex items-center justify-between gap-3">
                    <div className="flex items-center gap-2 font-semibold text-civic-heading">
                      <Building2 className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                      {borough.value}
                    </div>
                    <span className="text-sm text-civic-muted">{formatCompactNumber(borough.count)}</span>
                  </div>
                  <ScoreBar score={calculateBucketScore(borough.count, boroughRows[0]?.count ?? 1)} />
                </div>
              ))}
            </div>
          ) : (
            <div className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm font-semibold text-civic-muted">
              Historical borough analytics will appear after importing 311 records.
            </div>
          )}
        </Panel>

        <Panel title="Duplicate Decision Matrix" description="Live pgvector/PostGIS duplicate candidates grouped by score.">
          {duplicateRows.length ? (
            <div className="grid gap-3">
              {duplicateRows.map((item) => (
                <div className="rounded-md border border-civic-border bg-civic-raised p-4" key={item.label}>
                  <div className="mb-3 flex items-center justify-between">
                    <span className="font-medium text-civic-ink">{item.label}</span>
                    <span className="text-sm text-civic-muted">{item.cases} cases</span>
                  </div>
                  <ScoreBar score={item.score} />
                </div>
              ))}
            </div>
          ) : (
            <EmptyState label="Duplicate matrix appears after backend duplicate candidates are created." />
          )}
        </Panel>
      </div>
    </div>
  );
}

function buildCategoryRows(
  summary: HistoricalComplaintSummaryDto | null,
  liveIncidents: IncidentDto[],
  predictions: Map<string, TriagePredictionDto>,
): CategoryChartRow[] {
  if (!summary?.topCategories.length || summary.totalCount === 0) {
    const liveRows = countBy(
      liveIncidents.map((incident) => incident.correctedCategory ?? predictions.get(incident.id)?.category ?? inferCategory(incident.description)),
    );
    const maxCount = Math.max(...liveRows.map((item) => item.count), 1);

    return liveRows.map((item) => ({
      count: item.count,
      label: item.label,
      value: Math.round((item.count / Math.max(liveIncidents.length, 1)) * 100),
      width: calculateBucketScore(item.count, maxCount),
    }));
  }

  const rows = summary.topCategories.slice(0, 5);
  const maxCount = Math.max(...rows.map((item) => item.count), 1);

  return rows.map((item) => ({
    count: item.count,
    label: item.value,
    value: Math.round((item.count / summary.totalCount) * 100),
    width: calculateBucketScore(item.count, maxCount),
  }));
}

function buildAgencyPressureRows(
  summary: HistoricalComplaintSummaryDto | null,
  liveIncidents: IncidentDto[],
  predictions: Map<string, TriagePredictionDto>,
) {
  if (!summary?.topAgencies.length) {
    const liveRows = countBy(
      liveIncidents.map((incident) => {
        const prediction = predictions.get(incident.id);
        const category = incident.correctedCategory ?? prediction?.category ?? inferCategory(incident.description);

        return incident.assignedAgencyCode ?? incident.correctedAgencyCode ?? prediction?.suggestedAgencyCode ?? inferAgency(category);
      }),
    );
    const maxCount = Math.max(...liveRows.map((item) => item.count), 1);

    return liveRows.map((item) => ({
      agency: item.label,
      capacity: Math.max(item.count, Math.ceil(item.count * 1.35)),
      open: item.count,
      slaRisk: Math.min(96, Math.max(18, calculateBucketScore(item.count, maxCount))),
    }));
  }

  const rows = summary.topAgencies.slice(0, 4);
  const maxCount = Math.max(...rows.map((item) => item.count), 1);

  return rows.map((item) => {
    const pressure = calculateBucketScore(item.count, maxCount);

    return {
      agency: item.value,
      capacity: Math.max(item.count, Math.ceil(item.count * 1.35)),
      open: item.count,
      slaRisk: Math.min(96, Math.max(18, Math.round(pressure))),
    };
  });
}

function buildDuplicateRows(duplicates: DuplicateCandidateDto[]) {
  if (!duplicates.length) {
    return [];
  }

  const buckets = [
    { label: "High similarity", candidates: duplicates.filter((item) => item.similarityScore >= 0.85) },
    { label: "Review threshold", candidates: duplicates.filter((item) => item.similarityScore >= 0.7 && item.similarityScore < 0.85) },
    { label: "Low confidence match", candidates: duplicates.filter((item) => item.similarityScore < 0.7) },
  ].filter((bucket) => bucket.candidates.length);

  return buckets.map((bucket) => ({
    cases: bucket.candidates.length,
    label: bucket.label,
    score: Math.round(
      (bucket.candidates.reduce((total, candidate) => total + candidate.similarityScore, 0) / bucket.candidates.length) * 100,
    ),
  }));
}

function ForecastStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-surface p-3">
      <div className="text-xs font-semibold uppercase text-civic-muted">{label}</div>
      <div className="mt-1 text-xl font-semibold text-civic-heading">{value}</div>
    </div>
  );
}

function ForecastBar({
  label,
  max,
  tone,
  value,
}: {
  label: string;
  max: number;
  tone: "history" | "forecast";
  value: number;
}) {
  const height = Math.max(10, Math.min(100, (value / max) * 100));
  const color = tone === "history" ? "bg-civic-primary/45" : "bg-civic-primary";

  return (
    <div className="flex min-w-0 flex-col items-center gap-2">
      <div className="flex h-44 w-full items-end">
        <div className={`w-full rounded-t-md ${color} transition-all duration-500`} style={{ height: `${height}%` }} title={`${label}: ${Math.round(value)}`} />
      </div>
      <span className="hidden text-[11px] font-medium text-civic-muted sm:block">{label}</span>
    </div>
  );
}

function formatShortDate(value: string) {
  const [, month, day] = value.split("-");

  return `${month}/${day}`;
}

function calculateBucketScore(count: number, maxCount: number) {
  return Math.max(8, Math.min(100, Math.round((count / Math.max(maxCount, 1)) * 100)));
}

function formatCompactNumber(value: number) {
  return new Intl.NumberFormat("en", {
    compactDisplay: "short",
    maximumFractionDigits: 1,
    notation: "compact",
  }).format(value);
}

function historicalStatusLabel(status: "loading" | "ready" | "empty" | "fallback") {
  if (status === "loading") {
    return "Loading public records";
  }

  if (status === "ready") {
    return "Imported public data";
  }

  if (status === "empty") {
    return "No public records imported";
  }

  return "Historical API unavailable";
}

function incidentStatusLabel(status: "loading" | "ready" | "empty" | "fallback") {
  if (status === "loading") {
    return "Loading live incidents";
  }

  if (status === "ready") {
    return "Live incident API";
  }

  if (status === "empty") {
    return "No live incidents yet";
  }

  return "Incident API unavailable";
}

function countBy(values: string[]) {
  const counts = new Map<string, number>();

  for (const value of values.filter(Boolean)) {
    counts.set(value, (counts.get(value) ?? 0) + 1);
  }

  return Array.from(counts.entries())
    .map(([label, count]) => ({ count, label }))
    .sort((left, right) => right.count - left.count || left.label.localeCompare(right.label));
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

function EmptyState({ label }: { label: string }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm font-semibold text-civic-muted">
      {label}
    </div>
  );
}
