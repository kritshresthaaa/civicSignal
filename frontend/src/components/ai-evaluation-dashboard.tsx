"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import {
  Activity,
  AlertTriangle,
  BarChart3,
  BrainCircuit,
  CheckCircle2,
  CircleDashed,
  Database,
  GitCompareArrows,
  Loader2,
  RefreshCw,
  ShieldCheck,
  Sparkles,
} from "lucide-react";
import { readStoredAdminAccessToken } from "@/lib/admin-auth";
import {
  CivicApiError,
  getAiEvaluationBaselines,
  type AiEvaluationBaselineReportDto,
  type AiEvaluationGateDto,
  type AiEvaluationMetricDto,
  type AiEvaluationMetricGroupDto,
} from "@/lib/civic-api";
import { MetricCard, PageHeader, Panel, ScoreBar, SegmentedControl } from "@/components/ui-kit";

const views = ["Overview", "Quality Gates", "Model Runs", "Comparisons"] as const;
type View = (typeof views)[number];
type LoadState = "loading" | "live" | "fallback" | "error";

const fallbackReport: AiEvaluationBaselineReportDto = {
  baselineName: "CivicSignal deterministic baseline",
  comparisons: [
    {
      baseline: "Rule-based deterministic classifier",
      capability: "Text triage",
      decisionRule: "Promote only if macro F1 improves without lowering agency accuracy.",
      futureTarget: "Hugging Face or OpenAI text model",
    },
    {
      baseline: "pgvector text embeddings plus geospatial filter",
      capability: "Duplicate detection",
      decisionRule: "Promote only if recall improves while false-merge rate remains below 2%.",
      futureTarget: "Remote embedding model variants",
    },
    {
      baseline: "Fixture-level label agreement checks",
      capability: "Image analysis",
      decisionRule: "Promote only after reviewed media samples clear precision and recall gates.",
      futureTarget: "Vision model endpoint",
    },
    {
      baseline: "Moving-average/trend baseline",
      capability: "Forecasting",
      decisionRule: "Promote only if holdout MAPE and RMSE improve.",
      futureTarget: "Time-series model",
    },
  ],
  fixtureCounts: [
    { count: 27, name: "Classification cases" },
    { count: 7, name: "Duplicate queries" },
    { count: 21, name: "Forecast history days" },
    { count: 5, name: "Audio cases" },
    { count: 5, name: "Image cases" },
    { count: 3, name: "Generated report cases" },
  ],
  gates: [
    {
      category: "Classification",
      isHigherBetter: true,
      name: "Category routing gate",
      passed: true,
      rationale: "Category macro F1 must stay above the auto-routing threshold.",
      threshold: 90,
      unit: "%",
      value: 96.7,
    },
    {
      category: "Duplicates",
      isHigherBetter: false,
      name: "Duplicate safety gate",
      passed: true,
      rationale: "False merges must remain rare because merging unrelated reports is operationally risky.",
      threshold: 2,
      unit: "%",
      value: 0,
    },
    {
      category: "Forecasting",
      isHigherBetter: false,
      name: "Forecast workload gate",
      passed: true,
      rationale: "Forecast MAPE must remain low enough for staffing previews.",
      threshold: 8,
      unit: "%",
      value: 3.5,
    },
    {
      category: "Images",
      isHigherBetter: true,
      name: "Vision review gate",
      passed: true,
      rationale: "Image F1 must clear reviewer-assist quality before model promotion.",
      threshold: 84,
      unit: "%",
      value: 88.9,
    },
  ],
  generatedAt: "2026-07-24T20:18:17+00:00",
  metricGroups: [
    {
      metrics: [
        { isHigherBetter: true, name: "Category macro F1", passed: true, threshold: 90, unit: "%", value: 96.7 },
        { isHigherBetter: true, name: "Agency accuracy", passed: true, threshold: 90, unit: "%", value: 96.3 },
        { isHigherBetter: true, name: "Severity macro F1", passed: true, threshold: 85, unit: "%", value: 91 },
      ],
      name: "Classification",
      summary: "Category, severity, and agency-routing quality for incident triage.",
    },
    {
      metrics: [
        { isHigherBetter: true, name: "F1", passed: true, threshold: 90, unit: "%", value: 100 },
        { isHigherBetter: false, name: "False-merge rate", passed: true, threshold: 2, unit: "%", value: 0 },
      ],
      name: "Duplicate Detection",
      summary: "pgvector and geospatial duplicate scoring with false-merge control.",
    },
    {
      metrics: [
        { isHigherBetter: true, name: "Image F1", passed: true, threshold: 84, unit: "%", value: 88.9 },
        { isHigherBetter: false, name: "Unsupported-claim rate", passed: true, threshold: 8, unit: "%", value: 6.2 },
      ],
      name: "Multimodal",
      summary: "Vision and generated-report gates for future AI services.",
    },
  ],
  modelRuns: [
    {
      evaluatedAt: "2026-07-24T20:18:17+00:00",
      modelVersion: "2026.07.24",
      name: "Local deterministic baseline",
      notes: "Current repeatable benchmark in evaluation/reports/baseline-results.md.",
      provider: "CivicSignal",
      status: "Passed",
    },
    {
      evaluatedAt: null,
      modelVersion: "Planned",
      name: "Hugging Face multimodal service",
      notes: "Future Python service can compete against the same gates before production use.",
      provider: "Hugging Face",
      status: "Not connected",
    },
    {
      evaluatedAt: null,
      modelVersion: "Planned",
      name: "OpenAI report evaluator",
      notes: "Optional evaluator for generated-work-order factuality and unsupported-claim checks.",
      provider: "OpenAI",
      status: "Not connected",
    },
  ],
  nextUpgrades: [
    "Store evaluation runs, model versions, prompt versions, and gate outcomes in PostgreSQL.",
    "Add reviewed incident-media samples from real uploads.",
    "Compare Hugging Face and OpenAI model runs against the same fixed baseline fixtures.",
  ],
  reportVersion: "2026.07.24",
  summary: "Repeatable local benchmark for classification, duplicate detection, forecasting, audio, vision, and generated-report quality.",
};

export function AiEvaluationDashboard() {
  const [view, setView] = useState<View>("Overview");
  const [report, setReport] = useState<AiEvaluationBaselineReportDto>(fallbackReport);
  const [loadState, setLoadState] = useState<LoadState>("loading");
  const [message, setMessage] = useState("Loading evaluation baselines from the backend API...");

  const passedGates = report.gates.filter((gate) => gate.passed).length;
  const totalMetrics = report.metricGroups.reduce((total, group) => total + group.metrics.length, 0);
  const failedMetrics = report.metricGroups.reduce(
    (total, group) => total + group.metrics.filter((metric) => !metric.passed).length,
    0,
  );
  const connectedRuns = report.modelRuns.filter((run) => run.status === "Passed").length;
  const topMetricGroups = useMemo(() => report.metricGroups.slice(0, 4), [report.metricGroups]);

  const loadReport = useCallback(async () => {
    const accessToken = readStoredAdminAccessToken() ?? undefined;
    setLoadState("loading");
    setMessage("Loading evaluation baselines from the backend API...");

    try {
      const loadedReport = await getAiEvaluationBaselines(accessToken);
      setReport(loadedReport);
      setLoadState("live");
      setMessage(`${loadedReport.baselineName} loaded from the API.`);
    } catch (error) {
      setReport(fallbackReport);
      setLoadState(error instanceof CivicApiError ? "error" : "fallback");
      setMessage(error instanceof CivicApiError ? `${error.message}. Showing local baseline preview.` : "Backend unavailable. Showing local baseline preview.");
    }
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadReport();
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadReport]);

  return (
    <div className="space-y-6">
      <PageHeader
        actions={
          <div className="flex flex-wrap gap-2">
            <SegmentedControl onChange={setView} options={views} value={view} />
            <button
              className="inline-flex h-11 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface px-3 text-sm font-semibold text-civic-primary transition hover:bg-white"
              onClick={() => void loadReport()}
              type="button"
            >
              {loadState === "loading" ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <RefreshCw className="h-4 w-4" aria-hidden="true" />}
              Refresh
            </button>
          </div>
        }
        description="Model quality cockpit for baseline metrics, promotion gates, and future Hugging Face/OpenAI comparisons."
        eyebrow="AI Operations"
        title="AI Evaluation"
      />

      <div
        className={`flex flex-col gap-3 rounded-lg border p-4 sm:flex-row sm:items-center sm:justify-between ${
          loadState === "live"
            ? "border-status-approved bg-status-approved/20"
            : loadState === "loading"
              ? "border-status-review bg-status-review/20"
              : "border-status-critical bg-status-critical/10"
        }`}
      >
        <div className="flex items-center gap-3">
          <span className="rounded-md bg-civic-soft p-2 text-civic-primary">
            <BrainCircuit className="h-4 w-4" aria-hidden="true" />
          </span>
          <span>
            <span className="block text-sm font-semibold text-civic-heading">{loadStateLabel(loadState)}</span>
            <span className="mt-1 block text-sm text-civic-muted">{message}</span>
          </span>
        </div>
        <span className="rounded-md bg-civic-soft px-3 py-2 text-sm font-semibold text-civic-primary">
          Version {report.reportVersion}
        </span>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard icon={<ShieldCheck className="h-5 w-5" />} label="Quality gates" tone="calm" trend="Passed baseline checks" value={`${passedGates}/${report.gates.length}`} />
        <MetricCard icon={<BarChart3 className="h-5 w-5" />} label="Metrics tracked" trend="Across model capabilities" value={String(totalMetrics)} />
        <MetricCard icon={<AlertTriangle className="h-5 w-5" />} label="Metric failures" tone={failedMetrics ? "alert" : "calm"} trend="Current baseline risk" value={String(failedMetrics)} />
        <MetricCard icon={<Sparkles className="h-5 w-5" />} label="Connected runs" tone="review" trend="Ready for comparison" value={`${connectedRuns}/${report.modelRuns.length}`} />
      </div>

      {view === "Overview" ? (
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1.1fr)_minmax(360px,0.9fr)]">
          <Panel title="Baseline Scorecard" description={report.summary}>
            <div className="grid gap-4">
              {topMetricGroups.map((group) => (
                <MetricGroupPreview group={group} key={group.name} />
              ))}
            </div>
          </Panel>

          <Panel title="Fixture Coverage" description={`Generated ${formatDateTime(report.generatedAt)} from fixed evaluation fixtures.`}>
            <div className="grid gap-3">
              {report.fixtureCounts.map((fixture) => (
                <div className="flex items-center justify-between rounded-md border border-civic-border bg-civic-raised p-3" key={fixture.name}>
                  <span className="font-semibold text-civic-heading">{fixture.name}</span>
                  <span className="rounded-md bg-civic-soft px-3 py-1 text-sm font-semibold text-civic-primary">{fixture.count}</span>
                </div>
              ))}
            </div>
          </Panel>
        </div>
      ) : null}

      {view === "Quality Gates" ? (
        <Panel title="Promotion Gates" description="A future AI model should clear these gates before replacing the local deterministic baseline.">
          <div className="grid gap-4 lg:grid-cols-2">
            {report.gates.map((gate) => (
              <GateCard gate={gate} key={gate.name} />
            ))}
          </div>
        </Panel>
      ) : null}

      {view === "Model Runs" ? (
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(340px,0.85fr)]">
          <Panel title="Run Registry" description="Current baseline and planned external model runs.">
            <div className="grid gap-4">
              {report.modelRuns.map((run) => (
                <div className="rounded-lg border border-civic-border bg-civic-raised p-4" key={`${run.provider}-${run.name}`}>
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                    <div>
                      <div className="flex items-center gap-2 font-semibold text-civic-heading">
                        {run.status === "Passed" ? (
                          <CheckCircle2 className="h-4 w-4 text-status-approved-text" aria-hidden="true" />
                        ) : (
                          <CircleDashed className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                        )}
                        {run.name}
                      </div>
                      <p className="mt-2 text-sm leading-6 text-civic-muted">{run.notes}</p>
                    </div>
                    <div className="grid shrink-0 gap-2 text-sm">
                      <span className="rounded-md bg-civic-soft px-3 py-2 font-semibold text-civic-primary">{run.provider}</span>
                      <span className={runStatusClassName(run.status)}>{run.status}</span>
                    </div>
                  </div>
                  <div className="mt-4 grid gap-3 sm:grid-cols-2">
                    <RunFact label="Model version" value={run.modelVersion} />
                    <RunFact label="Evaluated" value={run.evaluatedAt ? formatDateTime(run.evaluatedAt) : "Pending"} />
                  </div>
                </div>
              ))}
            </div>
          </Panel>

          <Panel title="Next Upgrades" description="Work needed before external model promotion.">
            <div className="grid gap-3">
              {report.nextUpgrades.map((upgrade, index) => (
                <div className="grid grid-cols-[32px_minmax(0,1fr)] gap-3 rounded-md border border-civic-border bg-civic-raised p-3" key={upgrade}>
                  <span className="flex h-8 w-8 items-center justify-center rounded-md bg-civic-soft text-sm font-semibold text-civic-primary">
                    {index + 1}
                  </span>
                  <span className="text-sm leading-6 text-civic-muted">{upgrade}</span>
                </div>
              ))}
            </div>
          </Panel>
        </div>
      ) : null}

      {view === "Comparisons" ? (
        <Panel title="Model Promotion Matrix" description="How future Hugging Face/OpenAI runs should compete against the baseline.">
          <div className="grid gap-4">
            {report.comparisons.map((comparison) => (
              <div className="grid gap-4 rounded-lg border border-civic-border bg-civic-raised p-4 xl:grid-cols-[180px_minmax(0,1fr)_minmax(0,1fr)]" key={comparison.capability}>
                <div>
                  <div className="flex items-center gap-2 font-semibold text-civic-heading">
                    <GitCompareArrows className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                    {comparison.capability}
                  </div>
                </div>
                <ComparisonColumn icon={<Database className="h-4 w-4" />} label="Baseline" value={comparison.baseline} />
                <ComparisonColumn icon={<Activity className="h-4 w-4" />} label="Future target" value={comparison.futureTarget} detail={comparison.decisionRule} />
              </div>
            ))}
          </div>
        </Panel>
      ) : null}
    </div>
  );
}

function MetricGroupPreview({ group }: { group: AiEvaluationMetricGroupDto }) {
  const primaryMetric = group.metrics[0];
  return (
    <div className="rounded-lg border border-civic-border bg-civic-raised p-4">
      <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h3 className="font-semibold text-civic-heading">{group.name}</h3>
          <p className="mt-1 text-sm leading-6 text-civic-muted">{group.summary}</p>
        </div>
        {primaryMetric ? (
          <span className="rounded-md bg-civic-soft px-3 py-2 text-sm font-semibold text-civic-primary">
            {formatMetricValue(primaryMetric)}
          </span>
        ) : null}
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        {group.metrics.slice(0, 4).map((metric) => (
          <MetricMini metric={metric} key={metric.name} />
        ))}
      </div>
    </div>
  );
}

function MetricMini({ metric }: { metric: AiEvaluationMetricDto }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-surface p-3">
      <div className="mb-2 flex items-center justify-between gap-3">
        <span className="text-sm font-semibold text-civic-heading">{metric.name}</span>
        <span className={metric.passed ? "text-sm font-semibold text-status-approved-text" : "text-sm font-semibold text-status-critical-text"}>
          {formatMetricValue(metric)}
        </span>
      </div>
      <ScoreBar score={metric.unit === "%" ? metric.value : normalizedMetricScore(metric)} />
    </div>
  );
}

function GateCard({ gate }: { gate: AiEvaluationGateDto }) {
  return (
    <div className="rounded-lg border border-civic-border bg-civic-raised p-4">
      <div className="mb-4 flex items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 font-semibold text-civic-heading">
            {gate.passed ? (
              <CheckCircle2 className="h-4 w-4 text-status-approved-text" aria-hidden="true" />
            ) : (
              <AlertTriangle className="h-4 w-4 text-status-critical-text" aria-hidden="true" />
            )}
            {gate.name}
          </div>
          <p className="mt-2 text-sm leading-6 text-civic-muted">{gate.rationale}</p>
        </div>
        <span className={gate.passed ? "rounded-md bg-status-approved px-3 py-2 text-sm font-semibold text-status-approved-text" : "rounded-md bg-status-critical px-3 py-2 text-sm font-semibold text-status-critical-text"}>
          {gate.passed ? "Passed" : "Failed"}
        </span>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        <RunFact label="Current" value={`${gate.value}${gate.unit}`} />
        <RunFact label="Threshold" value={`${gate.isHigherBetter ? ">=" : "<="} ${gate.threshold}${gate.unit}`} />
        <RunFact label="Category" value={gate.category} />
      </div>
      <div className="mt-4">
        <ScoreBar label="Gate margin" score={gateScore(gate)} />
      </div>
    </div>
  );
}

function RunFact({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-surface p-3">
      <div className="text-xs font-semibold uppercase text-civic-muted">{label}</div>
      <div className="mt-1 break-words text-sm font-semibold text-civic-heading">{value}</div>
    </div>
  );
}

function ComparisonColumn({
  detail,
  icon,
  label,
  value,
}: {
  detail?: string;
  icon: ReactNode;
  label: string;
  value: string;
}) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-surface p-3">
      <div className="mb-2 flex items-center gap-2 text-sm font-semibold text-civic-primary">
        {icon}
        {label}
      </div>
      <p className="text-sm leading-6 text-civic-heading">{value}</p>
      {detail ? <p className="mt-2 text-sm leading-6 text-civic-muted">{detail}</p> : null}
    </div>
  );
}

function formatMetricValue(metric: AiEvaluationMetricDto) {
  if (metric.unit === "%") {
    return `${metric.value.toFixed(metric.value % 1 === 0 ? 0 : 1)}%`;
  }

  return `${metric.value.toLocaleString()} ${metric.unit}`;
}

function normalizedMetricScore(metric: AiEvaluationMetricDto) {
  if (!metric.threshold || metric.threshold <= 0) {
    return Math.min(100, metric.value);
  }

  if (metric.isHigherBetter) {
    return Math.min(100, (metric.value / metric.threshold) * 100);
  }

  return Math.max(0, Math.min(100, 100 - (metric.value / metric.threshold) * 100));
}

function gateScore(gate: AiEvaluationGateDto) {
  if (gate.isHigherBetter) {
    return Math.min(100, (gate.value / Math.max(gate.threshold, 1)) * 100);
  }

  return Math.max(0, Math.min(100, 100 - (gate.value / Math.max(gate.threshold, 1)) * 100));
}

function runStatusClassName(status: string) {
  if (status === "Passed") {
    return "rounded-md bg-status-approved px-3 py-2 font-semibold text-status-approved-text";
  }

  return "rounded-md bg-status-submitted px-3 py-2 font-semibold text-status-submitted-text";
}

function loadStateLabel(state: LoadState) {
  if (state === "loading") {
    return "Loading baseline";
  }

  if (state === "live") {
    return "Backend baseline";
  }

  if (state === "error") {
    return "API fallback";
  }

  return "Local preview";
}

function formatDateTime(value: string) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "Date unavailable";
  }

  return date.toLocaleString([], {
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    month: "short",
    year: "numeric",
  });
}
