"use client";

import {
  AlertTriangle,
  Bot,
  CheckCircle2,
  ClipboardCheck,
  CloudSun,
  FileText,
  Loader2,
  Play,
  ShieldQuestion,
  Sparkles,
  Workflow,
  XCircle,
} from "lucide-react";
import type { ReactNode } from "react";
import { isIncidentId, type ControlledTriageWorkflowDto } from "@/lib/civic-api";
import type { IncidentRow } from "@/lib/civic-types";
import { ScoreBar } from "@/components/ui-kit";

export type AgentWorkflowRunState = "idle" | "running" | "ready" | "error";

const evaluationGates = [
  { label: "Category F1", value: 96.7, detail: "classification" },
  { label: "Duplicate F1", value: 100, detail: "pgvector" },
  { label: "Image F1", value: 88.9, detail: "vision" },
  { label: "Report consistency", value: 93.3, detail: "drafts" },
];

export function AgentWorkflowPanel({
  incident,
  message,
  onRun,
  runState,
  workflow,
}: {
  incident: IncidentRow;
  message?: string;
  onRun: () => void;
  runState: AgentWorkflowRunState;
  workflow?: ControlledTriageWorkflowDto;
}) {
  const canRun = isIncidentId(incident.id);
  const weather = workflow?.weather;
  const draftWorkOrder = workflow?.draftWorkOrder;
  const toolRuns = workflow?.toolRuns ?? [];

  return (
    <div className="grid gap-5">
      <div className="overflow-hidden rounded-lg border border-civic-border bg-civic-raised">
        <div className="grid gap-4 p-4 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center">
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <span className="inline-flex items-center gap-2 rounded-md bg-civic-soft px-3 py-2 text-sm font-semibold text-civic-primary">
                <Bot className="h-4 w-4" aria-hidden="true" />
                Controlled Agent
              </span>
              <span className={`rounded-md border px-3 py-2 text-sm font-semibold ${workflowStatusClassName(workflow, runState)}`}>
                {workflowStatusLabel(workflow, runState)}
              </span>
            </div>
            <p className="mt-3 text-sm leading-6 text-civic-muted">
              {message ??
                (canRun
                  ? "Run the backend-controlled workflow for policy checks, weather context, duplicate review, and work-order drafting."
                  : "Select a live backend incident to run the controlled workflow.")}
            </p>
          </div>

          <button
            className="inline-flex h-11 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-sm font-semibold text-white transition hover:bg-civic-primary-strong disabled:cursor-not-allowed disabled:bg-civic-muted"
            disabled={!canRun || runState === "running"}
            onClick={onRun}
            type="button"
          >
            {runState === "running" ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <Play className="h-4 w-4" aria-hidden="true" />}
            Run Agent
          </button>
        </div>

        <div className="grid border-t border-civic-border md:grid-cols-3">
          <AgentStat
            icon={<Workflow className="h-4 w-4" />}
            label="Decision"
            value={workflow ? statusText(workflow.status) : "Not run"}
            detail={workflow?.reviewReason ?? `${incident.agency} routing candidate`}
          />
          <AgentStat
            icon={<AlertTriangle className="h-4 w-4" />}
            label="SLA risk"
            value={workflow ? `${Math.round(workflow.slaRisk * 100)}%` : `${incident.slaRisk}%`}
            detail={incident.severity}
          />
          <AgentStat
            icon={<CloudSun className="h-4 w-4" />}
            label="Weather"
            value={weather?.isAvailable ? weather.summary || "Available" : weather ? "Unavailable" : "Pending"}
            detail={weather?.isAvailable ? weather.stationIdentifier || weather.provider : weather?.unavailableReason ?? "Provider standby"}
          />
        </div>
      </div>

      {draftWorkOrder ? (
        <div className="rounded-lg border border-civic-border bg-civic-raised p-4">
          <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
            <div>
              <div className="flex items-center gap-2 font-semibold text-civic-heading">
                <ClipboardCheck className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                {draftWorkOrder.title}
              </div>
              <p className="mt-2 text-sm leading-6 text-civic-muted">{draftWorkOrder.summary}</p>
            </div>
            <div className="flex shrink-0 flex-wrap gap-2">
              <span className="rounded-md bg-civic-soft px-3 py-2 text-sm font-semibold text-civic-primary">{draftWorkOrder.agencyCode}</span>
              <span className="rounded-md bg-status-review px-3 py-2 text-sm font-semibold text-status-review-text">{draftWorkOrder.priority}</span>
            </div>
          </div>
          <div className="mt-4 grid gap-2">
            {draftWorkOrder.evidence.slice(0, 4).map((item) => (
              <div className="rounded-md border border-civic-border bg-civic-surface p-3 text-sm leading-6 text-civic-muted" key={item}>
                {item}
              </div>
            ))}
          </div>
        </div>
      ) : workflow?.requiresHumanReview ? (
        <div className="rounded-lg border border-status-review bg-status-review/20 p-4">
          <div className="flex items-start gap-3">
            <ShieldQuestion className="mt-0.5 h-5 w-5 shrink-0 text-status-review-text" aria-hidden="true" />
            <div>
              <div className="font-semibold text-status-review-text">Human review required</div>
              <p className="mt-1 text-sm leading-6 text-civic-muted">{workflow.reviewReason ?? "The workflow did not create a draft work order."}</p>
            </div>
          </div>
        </div>
      ) : null}

      <div className="grid gap-5 xl:grid-cols-[minmax(0,1.1fr)_minmax(320px,0.9fr)]">
        <div className="rounded-lg border border-civic-border bg-civic-raised p-4">
          <div className="mb-4 flex items-center justify-between gap-3">
            <div>
              <h3 className="font-semibold text-civic-heading">Tool Trace</h3>
              <p className="mt-1 text-sm text-civic-muted">{toolRuns.length ? `${toolRuns.length} controlled steps recorded` : "Workflow has not been executed for this case."}</p>
            </div>
            <Sparkles className="h-5 w-5 text-civic-primary" aria-hidden="true" />
          </div>

          <div className="grid gap-3">
            {toolRuns.length ? (
              toolRuns.map((toolRun, index) => (
                <div className="grid gap-3 rounded-md border border-civic-border bg-civic-surface p-3 sm:grid-cols-[32px_minmax(0,1fr)]" key={`${toolRun.toolName}-${index}`}>
                  <span className={`flex h-8 w-8 items-center justify-center rounded-md ${toolStatusClassName(toolRun.status)}`}>
                    {toolRun.status === "Succeeded" ? <CheckCircle2 className="h-4 w-4" aria-hidden="true" /> : <XCircle className="h-4 w-4" aria-hidden="true" />}
                  </span>
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <span className="font-semibold text-civic-heading">{formatToolName(toolRun.toolName)}</span>
                      <span className="text-xs font-semibold text-civic-muted">{formatTimestamp(toolRun.completedAt)}</span>
                    </div>
                    <p className="mt-1 text-sm leading-6 text-civic-muted">{toolRun.outputSummary}</p>
                    {toolRun.confidence !== null && toolRun.confidence !== undefined ? (
                      <div className="mt-3">
                        <ScoreBar label="Step confidence" score={toolRun.confidence * 100} />
                      </div>
                    ) : null}
                  </div>
                </div>
              ))
            ) : (
              <div className="rounded-md border border-civic-border bg-civic-surface p-4 text-sm font-semibold text-civic-muted">
                No agent trace has been recorded yet.
              </div>
            )}
          </div>
        </div>

        <div className="rounded-lg border border-civic-border bg-civic-raised p-4">
          <div className="mb-4 flex items-center gap-2 font-semibold text-civic-heading">
            <FileText className="h-4 w-4 text-civic-primary" aria-hidden="true" />
            Evaluation Gates
          </div>
          <div className="grid gap-4">
            {evaluationGates.map((gate) => (
              <div className="rounded-md border border-civic-border bg-civic-surface p-3" key={gate.label}>
                <div className="mb-2 flex items-center justify-between gap-3">
                  <div>
                    <div className="text-sm font-semibold text-civic-heading">{gate.label}</div>
                    <div className="text-xs font-semibold uppercase text-civic-muted">{gate.detail}</div>
                  </div>
                  <span className="text-sm font-semibold text-civic-primary">{gate.value.toFixed(1)}%</span>
                </div>
                <ScoreBar score={gate.value} />
              </div>
            ))}
            <div className="rounded-md border border-civic-border bg-civic-surface p-3 text-sm leading-6 text-civic-muted">
              Audio WER 13.3%, generated-report unsupported claims 6.2%, and forecast MAPE 3.5% are tracked in the baseline report.
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function AgentStat({
  detail,
  icon,
  label,
  value,
}: {
  detail: string;
  icon: ReactNode;
  label: string;
  value: string;
}) {
  return (
    <div className="border-t border-civic-border p-4 first:border-t-0 md:border-l md:border-t-0 md:first:border-l-0">
      <div className="flex items-center gap-2 text-sm text-civic-muted">
        <span className="text-civic-primary">{icon}</span>
        {label}
      </div>
      <div className="mt-2 line-clamp-2 font-semibold text-civic-heading">{value}</div>
      <div className="mt-1 line-clamp-2 text-sm text-civic-muted">{detail}</div>
    </div>
  );
}

function workflowStatusLabel(workflow: ControlledTriageWorkflowDto | undefined, runState: AgentWorkflowRunState) {
  if (runState === "running") {
    return "Running";
  }

  if (runState === "error") {
    return "Action failed";
  }

  if (!workflow) {
    return "Ready";
  }

  return workflow.requiresHumanReview ? "Review required" : "Draft ready";
}

function workflowStatusClassName(workflow: ControlledTriageWorkflowDto | undefined, runState: AgentWorkflowRunState) {
  if (runState === "running") {
    return "border-status-review bg-status-review/20 text-status-review-text";
  }

  if (runState === "error" || workflow?.requiresHumanReview) {
    return "border-status-critical bg-status-critical/10 text-status-critical-text";
  }

  if (workflow) {
    return "border-status-approved bg-status-approved/20 text-status-approved-text";
  }

  return "border-civic-border bg-civic-surface text-civic-muted";
}

function toolStatusClassName(status: string) {
  if (status === "Succeeded") {
    return "bg-status-approved text-status-approved-text";
  }

  if (status === "Unavailable" || status === "Skipped") {
    return "bg-status-review text-status-review-text";
  }

  return "bg-status-critical text-status-critical-text";
}

function statusText(value: string) {
  return value
    .split(/[_-]/g)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

function formatToolName(value: string) {
  return statusText(value);
}

function formatTimestamp(value: string) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "Recorded";
  }

  return date.toLocaleTimeString([], {
    hour: "numeric",
    minute: "2-digit",
  });
}
