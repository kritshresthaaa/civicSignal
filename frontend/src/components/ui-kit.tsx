import type { ReactNode } from "react";
import type { IncidentStatus, Severity } from "@/lib/civic-types";
import { statusLabel } from "@/lib/civic-analysis";

const statusStyles: Record<IncidentStatus, string> = {
  Submitted: "bg-status-submitted text-status-submitted-text",
  Triaged: "bg-status-triaged text-status-triaged-text",
  HumanReviewRequired: "bg-status-review text-status-review-text",
  Approved: "bg-status-approved text-status-approved-text",
  Dispatched: "bg-status-critical text-status-critical-text",
};

const severityStyles: Record<Severity, string> = {
  Low: "bg-status-approved text-status-approved-text",
  Medium: "bg-status-review text-status-review-text",
  High: "bg-status-triaged text-status-triaged-text",
  Critical: "bg-status-critical text-status-critical-text",
};

export const fieldClassName =
  "w-full rounded-md border border-civic-border bg-civic-surface px-3 py-3 text-base text-civic-ink outline-none transition placeholder:text-civic-muted/70 focus:border-civic-primary focus:ring-2 focus:ring-civic-primary/20";

export function PageHeader({
  actions,
  eyebrow,
  title,
  description,
}: {
  actions?: ReactNode;
  eyebrow?: string;
  title: string;
  description: string;
}) {
  return (
    <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
      <div>
        {eyebrow ? <p className="text-sm font-semibold uppercase tracking-[0.16em] text-civic-primary">{eyebrow}</p> : null}
        <h1 className="mt-2 text-3xl font-semibold tracking-normal text-civic-heading md:text-4xl">{title}</h1>
        <p className="mt-2 max-w-3xl text-sm leading-6 text-civic-muted md:text-base">{description}</p>
      </div>
      {actions ? <div className="flex flex-wrap gap-2">{actions}</div> : null}
    </div>
  );
}

export function Panel({
  children,
  className = "",
  title,
  description,
  action,
}: {
  children: ReactNode;
  className?: string;
  title?: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <section
      className={`animate-fade-up rounded-lg border border-civic-border bg-civic-surface p-5 shadow-sm transition duration-200 hover:border-civic-border-strong hover:shadow-md ${className}`}
    >
      {title || description || action ? (
        <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div>
            {title ? <h2 className="text-xl font-semibold text-civic-heading">{title}</h2> : null}
            {description ? <p className="mt-1 text-sm leading-6 text-civic-muted">{description}</p> : null}
          </div>
          {action ? <div className="shrink-0">{action}</div> : null}
        </div>
      ) : null}
      {children}
    </section>
  );
}

export function MetricCard({
  icon,
  label,
  value,
  trend,
  tone = "default",
}: {
  icon: ReactNode;
  label: string;
  value: string;
  trend: string;
  tone?: "default" | "alert" | "review" | "calm";
}) {
  const toneClass =
    tone === "alert"
      ? "bg-status-critical text-status-critical-text"
      : tone === "review"
        ? "bg-status-review text-status-review-text"
        : tone === "calm"
          ? "bg-status-approved text-status-approved-text"
          : "bg-civic-soft text-civic-primary";

  return (
    <div className="animate-fade-up group rounded-lg border border-civic-border bg-civic-surface p-5 shadow-sm transition duration-200 hover:-translate-y-0.5 hover:border-civic-border-strong hover:shadow-md">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-civic-muted">{label}</span>
        <span className={`rounded-md p-2 transition group-hover:scale-105 ${toneClass}`}>{icon}</span>
      </div>
      <div className="mt-4 text-3xl font-semibold tracking-normal text-civic-heading">{value}</div>
      <p className="mt-1 text-sm text-civic-muted">{trend}</p>
    </div>
  );
}

export function StatusBadge({ status }: { status: IncidentStatus }) {
  return <span className={`rounded-md px-2 py-1 text-xs font-semibold ${statusStyles[status]}`}>{statusLabel(status)}</span>;
}

export function SeverityBadge({ severity }: { severity: Severity }) {
  return <span className={`rounded-md px-2 py-1 text-xs font-semibold ${severityStyles[severity]}`}>{severity}</span>;
}

export function ScoreBar({ label, score }: { label?: string; score: number }) {
  const width = Math.min(100, Math.max(0, score));

  return (
    <div>
      <div className="flex items-center justify-between text-sm">
        {label ? <span className="font-medium text-civic-muted">{label}</span> : <span />}
        <span className="font-semibold text-civic-ink">{Math.round(score)}%</span>
      </div>
      <div className="mt-2 h-2 rounded-full bg-civic-border">
        <div className="h-2 rounded-full bg-civic-primary transition-all duration-500 ease-out" style={{ width: `${width}%` }} />
      </div>
    </div>
  );
}

export function SegmentedControl<T extends string>({
  options,
  value,
  onChange,
}: {
  options: readonly T[];
  value: T;
  onChange: (value: T) => void;
}) {
  return (
    <div
      className="inline-grid max-w-full overflow-x-auto rounded-md border border-civic-border bg-civic-raised p-1"
      style={{ gridTemplateColumns: `repeat(${options.length}, minmax(max-content, 1fr))` }}
    >
      {options.map((option) => (
        <button
          aria-pressed={value === option}
          className={`h-10 whitespace-nowrap rounded-md px-3 text-sm font-semibold transition ${
            value === option ? "bg-civic-primary text-white shadow-sm" : "text-civic-muted hover:bg-civic-soft hover:text-civic-primary"
          }`}
          key={option}
          onClick={() => onChange(option)}
          type="button"
        >
          {option}
        </button>
      ))}
    </div>
  );
}

export function IconButton({
  children,
  label,
  onClick,
}: {
  children: ReactNode;
  label: string;
  onClick?: () => void;
}) {
  return (
    <button
      aria-label={label}
      className="inline-flex h-10 w-10 items-center justify-center rounded-md border border-civic-border text-civic-primary transition hover:bg-civic-soft focus:ring-2 focus:ring-civic-primary/20"
      onClick={onClick}
      title={label}
      type="button"
    >
      {children}
    </button>
  );
}
