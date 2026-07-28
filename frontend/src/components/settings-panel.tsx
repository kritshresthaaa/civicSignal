"use client";

import { useCallback, useEffect, useState } from "react";
import type { ReactNode } from "react";
import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Database,
  HeartPulse,
  KeyRound,
  Loader2,
  RadioTower,
  RefreshCw,
  RotateCcw,
  ServerCog,
  Shield,
  SlidersHorizontal,
  ToggleLeft,
  ToggleRight,
} from "lucide-react";
import {
  CivicApiError,
  getSystemHealth,
  getSystemIntegrations,
  getSystemRuntimePolicy,
  type SystemHealthCheckDto,
  type SystemHealthResponse,
  type SystemIntegrationStatusDto,
  type SystemRuntimePolicyResponse,
} from "@/lib/civic-api";
import { PageHeader, Panel, ScoreBar, SegmentedControl } from "@/components/ui-kit";

const settingsTabs = ["Access", "AI Rules", "Integrations", "Health"] as const;
type LoadState = "loading" | "live" | "error";

export function SettingsPanel() {
  const [tab, setTab] = useState<(typeof settingsTabs)[number]>("AI Rules");
  const [state, setState] = useState<LoadState>("loading");
  const [message, setMessage] = useState("Loading backend runtime configuration...");
  const [integrations, setIntegrations] = useState<SystemIntegrationStatusDto[]>([]);
  const [health, setHealth] = useState<SystemHealthResponse | null>(null);
  const [policy, setPolicy] = useState<SystemRuntimePolicyResponse | null>(null);
  const [remoteEmbeddings, setRemoteEmbeddings] = useState(false);
  const [weatherContext, setWeatherContext] = useState(false);
  const [queueProcessing, setQueueProcessing] = useState(false);
  const [duplicateScore, setDuplicateScore] = useState(70);
  const [radius, setRadius] = useState(500);
  const [timeWindow, setTimeWindow] = useState(168);
  const [draftState, setDraftState] = useState("Controls mirror backend configuration.");

  const loadConfiguration = useCallback(async () => {
    setState("loading");
    setMessage("Loading backend runtime configuration...");

    try {
      const [loadedIntegrations, loadedPolicy] = await Promise.all([
        getSystemIntegrations(),
        getSystemRuntimePolicy(),
      ]);
      const loadedHealth = await getSystemHealth();

      setIntegrations(loadedIntegrations.integrations);
      setHealth(loadedHealth);
      setPolicy(loadedPolicy);
      applyPolicy(loadedPolicy, setDuplicateScore, setRadius, setTimeWindow, setRemoteEmbeddings, setWeatherContext, setQueueProcessing);
      setDraftState("Controls mirror backend configuration.");
      setState("live");
      setMessage(`${loadedIntegrations.integrations.length} integration states loaded from ${loadedIntegrations.service}.`);
    } catch (error) {
      setIntegrations([]);
      setHealth(null);
      setPolicy(null);
      setState("error");
      setMessage(error instanceof CivicApiError ? error.message : "Could not load backend runtime configuration.");
    }
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadConfiguration();
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadConfiguration]);

  function markDraftChange() {
    setDraftState("Local draft only. Backend runtime values come from appsettings or environment variables.");
  }

  function resetPolicyDraft() {
    if (!policy) {
      return;
    }

    applyPolicy(policy, setDuplicateScore, setRadius, setTimeWindow, setRemoteEmbeddings, setWeatherContext, setQueueProcessing);
    setDraftState("Controls mirror backend configuration.");
  }

  return (
    <div className="space-y-6">
      <PageHeader
        actions={<SegmentedControl onChange={setTab} options={settingsTabs} value={tab} />}
        description="Inspect backend authorization policy, duplicate detection settings, and integration configuration without exposing infrastructure credentials."
        eyebrow="Administration"
        title="Settings"
      />

      <Panel
        action={
          <button
            className="inline-flex h-10 items-center gap-2 rounded-md bg-civic-primary px-4 text-sm font-semibold text-white hover:bg-civic-primary-strong disabled:cursor-not-allowed disabled:opacity-60"
            disabled={state === "loading"}
            onClick={() => void loadConfiguration()}
            type="button"
          >
            {state === "loading" ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <RefreshCw className="h-4 w-4" aria-hidden="true" />}
            Refresh
          </button>
        }
        title="Configuration State"
        description={message}
      >
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <SettingTile icon={<Shield className="h-4 w-4" />} label="Access model" value="Identity + JWT + cookies" />
          <SettingTile icon={<SlidersHorizontal className="h-4 w-4" />} label="Duplicate threshold" value={`${duplicateScore}%`} />
          <SettingTile icon={<Database className="h-4 w-4" />} label="Embedding dimensions" value={policy ? String(policy.textEmbeddingDimensions) : "Loading"} />
          <SettingTile icon={<HeartPulse className="h-4 w-4" />} label="System health" value={health?.status ?? "Loading"} />
        </div>
      </Panel>

      {tab === "Access" ? (
        <Panel title="Access Management" description="These are the backend-enforced role and policy boundaries used by the API.">
          <div className="grid gap-4 lg:grid-cols-3">
            {[
              { role: "Public resident", access: "Create reports, upload evidence, and track own report by tracking code.", enforcement: "AllowAnonymous + public-write rate limit" },
              { role: "Reviewer", access: "Inspect evidence, duplicate candidates, review history, and submit corrections.", enforcement: "IncidentReview policy" },
              { role: "Operator/Admin", access: "Run analysis, processing updates, data imports, and controlled agent workflows.", enforcement: "IncidentOperations policy" },
            ].map((item) => (
              <div className="rounded-md border border-civic-border bg-civic-raised p-4" key={item.role}>
                <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
                  <KeyRound className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                  {item.role}
                </div>
                <p className="mt-3 text-sm leading-6 text-civic-muted">{item.access}</p>
                <p className="mt-4 rounded-md bg-civic-soft px-3 py-2 text-sm font-semibold text-civic-primary">{item.enforcement}</p>
              </div>
            ))}
          </div>
        </Panel>
      ) : null}

      {tab === "AI Rules" ? (
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
          <Panel
            action={
              <button
                className="inline-flex h-9 items-center gap-2 rounded-md border border-civic-border px-3 text-sm font-semibold text-civic-primary transition hover:bg-civic-soft disabled:cursor-not-allowed disabled:opacity-50"
                disabled={!policy}
                onClick={resetPolicyDraft}
                type="button"
              >
                <RotateCcw className="h-4 w-4" aria-hidden="true" />
                Reset
              </button>
            }
            title="Duplicate Detection Policy"
            description={draftState}
          >
            <div className="grid gap-6">
              <RangeControl
                label="Duplicate score threshold"
                max={98}
                min={40}
                onChange={(value) => {
                  setDuplicateScore(value);
                  markDraftChange();
                }}
                suffix="%"
                value={duplicateScore}
              />
              <RangeControl
                label="Geospatial radius"
                max={2000}
                min={50}
                onChange={(value) => {
                  setRadius(value);
                  markDraftChange();
                }}
                suffix="m"
                value={radius}
              />
              <RangeControl
                label="Time window"
                max={720}
                min={24}
                onChange={(value) => {
                  setTimeWindow(value);
                  markDraftChange();
                }}
                suffix="h"
                value={timeWindow}
              />
            </div>
          </Panel>

          <Panel title="Runtime Flags" description="These toggles mirror backend configuration and can be staged locally for discussion.">
            <div className="grid gap-3">
              <ToggleRow
                active={remoteEmbeddings}
                label="Remote AI embeddings"
                onClick={() => {
                  setRemoteEmbeddings((value) => !value);
                  markDraftChange();
                }}
              />
              <ToggleRow
                active={weatherContext}
                label="Weather context"
                onClick={() => {
                  setWeatherContext((value) => !value);
                  markDraftChange();
                }}
              />
              <ToggleRow
                active={queueProcessing}
                label="RabbitMQ queue processing"
                onClick={() => {
                  setQueueProcessing((value) => !value);
                  markDraftChange();
                }}
              />
            </div>
            <div className="mt-4 rounded-md border border-civic-border bg-civic-raised p-3 text-sm leading-6 text-civic-muted">
              Backend currently allows {policy?.duplicateMaxResults ?? "-"} duplicate results from a pool of {policy?.duplicateCandidatePoolSize ?? "-"} candidates.
            </div>
          </Panel>
        </div>
      ) : null}

      {tab === "Integrations" ? (
        <Panel title="Backend Integrations" description="Live status comes from the .NET API configuration, not local frontend constants.">
          {integrations.length ? (
            <div className="grid gap-3">
              {integrations.map((item) => (
                <div className="grid gap-3 rounded-md border border-civic-border bg-civic-raised p-4 md:grid-cols-[220px_minmax(0,1fr)_150px]" key={item.name}>
                  <div className="flex items-center gap-2 font-semibold text-civic-heading">
                    {item.enabled ? (
                      <ServerCog className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                    ) : (
                      <RadioTower className="h-4 w-4 text-civic-muted" aria-hidden="true" />
                    )}
                    {item.name}
                  </div>
                  <p className="text-sm leading-6 text-civic-muted">
                    <span className="font-semibold text-civic-heading">{item.category}:</span> {item.detail}
                  </p>
                  <span className={`inline-flex h-8 items-center justify-center rounded-md px-3 text-sm font-semibold ${integrationStatusClass(item.status, item.enabled)}`}>
                    {item.status}
                  </span>
                </div>
              ))}
            </div>
          ) : (
            <div className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm font-semibold text-civic-muted">
              Integration status is unavailable until the backend API responds.
            </div>
          )}
        </Panel>
      ) : null}

      {tab === "Health" ? (
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
          <Panel
            title="Operational Health"
            description={
              health
                ? `${health.service} reported ${health.status.toLowerCase()} at ${formatDateTime(health.generatedAt)}.`
                : "Health checks are unavailable until the backend API responds."
            }
          >
            {health?.checks.length ? (
              <div className="grid gap-3">
                {health.checks.map((check) => (
                  <HealthCheckRow check={check} key={`${check.category}-${check.name}`} />
                ))}
              </div>
            ) : (
              <div className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm font-semibold text-civic-muted">
                Run the backend and refresh this page to inspect database, storage, cache, queue, AI, and external API readiness.
              </div>
            )}
          </Panel>

          <Panel title="Demo Readiness" description="Use this panel before exposing the app to another device or recording a portfolio walkthrough.">
            <div className="grid gap-3">
              <ReadinessItem active={health?.status === "Healthy"} label="Backend is healthy" />
              <ReadinessItem active={Boolean(health?.checks.some((check) => check.name === "PostgreSQL connection" && check.status === "Healthy"))} label="Database is reachable" />
              <ReadinessItem active={Boolean(health?.checks.some((check) => check.name === "PostGIS and pgvector extensions" && check.status === "Healthy"))} label="Geo/vector extensions are ready" />
              <ReadinessItem active={Boolean(health?.checks.some((check) => check.name === "Request correlation"))} label="Trace correlation is enabled" />
            </div>
            <div className="mt-4 rounded-md border border-civic-border bg-civic-soft p-3 text-sm leading-6 text-civic-muted">
              Use `/api/system/health` for backend smoke checks and `/health/ready` for deployment readiness probes.
            </div>
          </Panel>
        </div>
      ) : null}
    </div>
  );
}

function applyPolicy(
  policy: SystemRuntimePolicyResponse,
  setDuplicateScore: (value: number) => void,
  setRadius: (value: number) => void,
  setTimeWindow: (value: number) => void,
  setRemoteEmbeddings: (value: boolean) => void,
  setWeatherContext: (value: boolean) => void,
  setQueueProcessing: (value: boolean) => void,
) {
  setDuplicateScore(Math.round(policy.duplicateMinimumScore * 100));
  setRadius(Math.round(policy.duplicateSearchRadiusMeters));
  setTimeWindow(policy.duplicateTimeWindowHours);
  setRemoteEmbeddings(policy.aiServiceEnabled && policy.remoteEmbeddingsEnabled);
  setWeatherContext(policy.weatherEnabled);
  setQueueProcessing(policy.rabbitMqEnabled);
}

function RangeControl({
  label,
  max,
  min,
  onChange,
  suffix,
  value,
}: {
  label: string;
  max: number;
  min: number;
  onChange: (value: number) => void;
  suffix: string;
  value: number;
}) {
  return (
    <div>
      <div className="mb-2 flex items-center justify-between">
        <label className="text-sm font-semibold text-civic-heading">{label}</label>
        <span className="text-sm font-semibold text-civic-primary">
          {value}
          {suffix}
        </span>
      </div>
      <input
        className="w-full accent-civic-primary"
        max={max}
        min={min}
        onChange={(event) => onChange(Number(event.target.value))}
        type="range"
        value={value}
      />
      <div className="mt-2">
        <ScoreBar score={((value - min) / (max - min)) * 100} />
      </div>
    </div>
  );
}

function ToggleRow({ active, label, onClick }: { active: boolean; label: string; onClick: () => void }) {
  return (
    <button
      aria-pressed={active}
      className="flex items-center justify-between rounded-md border border-civic-border bg-civic-raised p-4 text-left transition hover:border-civic-border-strong"
      onClick={onClick}
      type="button"
    >
      <span className="font-medium text-civic-ink">{label}</span>
      {active ? (
        <ToggleRight className="h-6 w-6 text-civic-primary" aria-hidden="true" />
      ) : (
        <ToggleLeft className="h-6 w-6 text-civic-muted" aria-hidden="true" />
      )}
    </button>
  );
}

function SettingTile({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-raised p-4">
      <div className="flex items-center gap-2 text-sm text-civic-muted">
        <span className="text-civic-primary">{icon}</span>
        {label}
      </div>
      <div className="mt-2 text-lg font-semibold text-civic-heading">{value}</div>
    </div>
  );
}

function HealthCheckRow({ check }: { check: SystemHealthCheckDto }) {
  return (
    <div className="grid gap-3 rounded-md border border-civic-border bg-civic-raised p-4 md:grid-cols-[220px_minmax(0,1fr)_150px]">
      <div>
        <div className="flex items-center gap-2 font-semibold text-civic-heading">
          <span className={healthStatusIconClass(check.status)}>
            {healthStatusIcon(check.status)}
          </span>
          {check.name}
        </div>
        <div className="mt-1 text-xs font-semibold uppercase text-civic-muted">{check.category}</div>
      </div>
      <p className="text-sm leading-6 text-civic-muted">
        {check.detail}
        {typeof check.latencyMilliseconds === "number" ? (
          <span className="ml-2 inline-flex items-center gap-1 font-semibold text-civic-primary">
            <Activity className="h-3.5 w-3.5" aria-hidden="true" />
            {check.latencyMilliseconds} ms
          </span>
        ) : null}
      </p>
      <span className={`inline-flex h-8 items-center justify-center rounded-md px-3 text-sm font-semibold ${healthStatusClass(check.status)}`}>
        {check.critical ? "Critical" : "Optional"} - {check.status}
      </span>
    </div>
  );
}

function ReadinessItem({ active, label }: { active: boolean; label: string }) {
  return (
    <div className="flex items-center justify-between rounded-md border border-civic-border bg-civic-raised p-3">
      <span className="text-sm font-semibold text-civic-heading">{label}</span>
      {active ? (
        <CheckCircle2 className="h-4 w-4 text-civic-primary" aria-hidden="true" />
      ) : (
        <AlertTriangle className="h-4 w-4 text-status-review-text" aria-hidden="true" />
      )}
    </div>
  );
}

function healthStatusIcon(status: string) {
  if (status === "Healthy" || status === "Configured") {
    return <CheckCircle2 className="h-4 w-4" aria-hidden="true" />;
  }

  return <AlertTriangle className="h-4 w-4" aria-hidden="true" />;
}

function healthStatusIconClass(status: string) {
  if (status === "Healthy" || status === "Configured") {
    return "text-civic-primary";
  }

  return "text-status-review-text";
}

function healthStatusClass(status: string) {
  if (status === "Healthy" || status === "Configured") {
    return "bg-status-triaged text-status-triaged-text";
  }

  if (status === "Disabled" || status === "Skipped") {
    return "bg-status-submitted text-status-submitted-text";
  }

  return "bg-status-review text-status-review-text";
}

function integrationStatusClass(status: string, enabled: boolean) {
  if (enabled) {
    return "bg-status-triaged text-status-triaged-text";
  }

  if (status === "Heuristic fallback" || status.includes("fallback") || status === "Local") {
    return "bg-status-review text-status-review-text";
  }

  return "bg-status-submitted text-status-submitted-text";
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}
