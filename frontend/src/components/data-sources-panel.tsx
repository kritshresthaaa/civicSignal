"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  CheckCircle2,
  Clock3,
  Database,
  DownloadCloud,
  History,
  KeyRound,
  Loader2,
  MapPin,
  Play,
  Radio,
  RefreshCw,
  RotateCcw,
  ShieldCheck,
  TriangleAlert,
} from "lucide-react";
import {
  apiBaseUrl,
  CivicApiError,
  getBackendCapabilities,
  getHistoricalComplaintSummary,
  login,
  logout,
  queueNyc311ImportJob,
  retryDataImportJob,
  searchDataImportJobs,
  searchHistoricalComplaints,
  type DataImportJobDto,
  type HistoricalComplaintDto,
  type HistoricalComplaintSummaryDto,
} from "@/lib/civic-api";
import { fieldClassName, MetricCard, PageHeader, Panel, ScoreBar, SegmentedControl } from "@/components/ui-kit";

const tokenStorageKey = "civicsignal-admin-token";
const tokenExpiresAtStorageKey = "civicsignal-admin-token-expires-at";
const tokenRefreshSkewMs = 60_000;
const statusFilters = ["All", "Pending", "Running", "Succeeded", "Failed"] as const;
const requiredDataSourceRoutes = [
  "api/historical-complaints/summary",
  "api/data-import-jobs",
  "api/data-import-jobs/nyc311",
];

type LoadState = "idle" | "loading" | "ready" | "error";

export function DataSourcesPanel() {
  const [accessToken, setAccessToken] = useState("");
  const [backendReady, setBackendReady] = useState(false);
  const [backendState, setBackendState] = useState<LoadState>("loading");
  const [backendMessage, setBackendMessage] = useState("Checking the backend routes required by this workspace.");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [authState, setAuthState] = useState<LoadState>("idle");
  const [authMessage, setAuthMessage] = useState("Operator access required for import jobs.");
  const [jobs, setJobs] = useState<DataImportJobDto[]>([]);
  const [jobsState, setJobsState] = useState<LoadState>("idle");
  const [summary, setSummary] = useState<HistoricalComplaintSummaryDto | null>(null);
  const [recentComplaints, setRecentComplaints] = useState<HistoricalComplaintDto[]>([]);
  const [lastRefreshedAt, setLastRefreshedAt] = useState<string | null>(null);
  const [status, setStatus] = useState<(typeof statusFilters)[number]>("All");
  const [limit, setLimit] = useState(500);
  const [daysBack, setDaysBack] = useState(30);
  const [borough, setBorough] = useState("All");
  const [complaintType, setComplaintType] = useState("");
  const [queueState, setQueueState] = useState<LoadState>("idle");
  const [queueMessage, setQueueMessage] = useState("No import job queued this session.");
  const [retryingJobId, setRetryingJobId] = useState<string | null>(null);

  const filteredJobs = useMemo(() => {
    return status === "All" ? jobs : jobs.filter((job) => job.status === status);
  }, [jobs, status]);
  const latestJob = jobs[0];
  const runningCount = jobs.filter((job) => job.status === "Pending" || job.status === "Running").length;
  const processedRecords = jobs.reduce((total, job) => total + job.receivedCount, 0);

  const expireSession = useCallback((message: string) => {
    setAccessToken("");
    setJobs([]);
    setJobsState("idle");
    setAuthState("error");
    setAuthMessage(message);
    clearStoredAccessToken();
  }, []);

  const verifyBackendRoutes = useCallback(async () => {
    setBackendState("loading");
    setBackendMessage("Checking Data Sources backend routes...");

    try {
      const capabilities = await getBackendCapabilities();
      const availableRoutes = new Set(capabilities.routes.map(normalizeRoute));
      const missingRoutes = requiredDataSourceRoutes.filter((route) => !availableRoutes.has(route));

      if (missingRoutes.length > 0) {
        setBackendReady(false);
        setBackendState("error");
        setBackendMessage(
          `The API at ${apiBaseUrl} is running, but it is missing ${missingRoutes.join(", ")}. Rebuild and restart the backend so the latest controllers are loaded.`,
        );
        return false;
      }

      setBackendReady(true);
      setBackendState("ready");
      setBackendMessage(`Connected to ${capabilities.service} with Data Sources routes available.`);
      return true;
    } catch (error) {
      setBackendReady(false);
      setBackendState("error");
      setBackendMessage(buildBackendReadinessMessage(error));
      return false;
    }
  }, []);

  const refreshJobs = useCallback(
    async (token = accessToken || undefined) => {
      setJobsState("loading");

      try {
        const loadedJobs = await searchDataImportJobs({ pageSize: 30, source: "NYC311" }, token);
        setJobs(loadedJobs);
        setJobsState("ready");
        setLastRefreshedAt(new Date().toISOString());
      } catch (error) {
        if (error instanceof CivicApiError && error.status === 401) {
          expireSession("Your saved operator session expired. Sign in again.");
          setQueueMessage("Session expired. Sign in again to load import jobs.");
          return;
        }

        setJobsState("error");
        setQueueMessage(error instanceof CivicApiError ? error.message : "Could not load import job history.");
      }
    },
    [accessToken, expireSession],
  );

  const refreshHistoricalData = useCallback(async () => {
    try {
      const [loadedSummary, loadedComplaints] = await Promise.all([
        getHistoricalComplaintSummary({ pageSize: 1 }),
        searchHistoricalComplaints({ pageSize: 6 }),
      ]);

      setSummary(loadedSummary);
      setRecentComplaints(loadedComplaints);
      setLastRefreshedAt(new Date().toISOString());
    } catch {
      setSummary(null);
      setRecentComplaints([]);
    }
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void (async () => {
        const routesAvailable = await verifyBackendRoutes();

        if (!routesAvailable) {
          return;
        }

        const storedToken = readStoredAccessToken();

        if (storedToken) {
          setAccessToken(storedToken);
          setAuthState("ready");
          setAuthMessage("Operator token loaded for this browser.");
          void refreshJobs(storedToken);
        } else {
          setAuthState("ready");
          setAuthMessage("Using the staff cookie session managed by the admin shell.");
          void refreshJobs();
        }

        void refreshHistoricalData();
      })();
    }, 0);

    return () => window.clearTimeout(timer);
  }, [refreshHistoricalData, refreshJobs, verifyBackendRoutes]);

  useEffect(() => {
    if (!backendReady) {
      return;
    }

    const timer = window.setInterval(() => {
      void refreshJobs();
      void refreshHistoricalData();
    }, runningCount > 0 ? 3_000 : 10_000);

    return () => window.clearInterval(timer);
  }, [accessToken, backendReady, refreshHistoricalData, refreshJobs, runningCount]);

  async function signIn() {
    const routesAvailable = backendReady || (await verifyBackendRoutes());
    if (!routesAvailable) {
      setAuthState("error");
      setAuthMessage("Data Sources backend routes are unavailable.");
      return;
    }

    setAuthState("loading");
    setAuthMessage("Signing in...");

    try {
      const token = await login(email.trim(), password);
      setAccessToken(token.accessToken);
      storeAccessToken(token.accessToken, token.expiresIn);
      setAuthState("ready");
      setAuthMessage(`Token ready for ${Math.round(token.expiresIn / 60)} min.`);
      await refreshJobs(token.accessToken);
      await refreshHistoricalData();
    } catch (error) {
      setAuthState("error");
      setAuthMessage(error instanceof CivicApiError ? error.message : "Sign-in failed.");
    }
  }

  function clearToken() {
    void logout().catch(() => undefined);
    setAccessToken("");
    setJobs([]);
    clearStoredAccessToken();
    setAuthState("idle");
    setAuthMessage("Operator access required for import jobs.");
    setQueueMessage("No import job queued this session.");
  }

  async function queueImport() {
    const routesAvailable = backendReady || (await verifyBackendRoutes());
    if (!routesAvailable) {
      setQueueState("error");
      setQueueMessage("Data Sources backend routes are unavailable.");
      return;
    }

    setQueueState("loading");
    setQueueMessage("Queueing NYC 311 import job...");

    try {
      const job = await queueNyc311ImportJob(
        {
          borough: borough === "All" ? null : borough,
          complaintType: complaintType.trim() || null,
          daysBack,
          limit,
        },
        accessToken || undefined,
      );

      setQueueState("ready");
      setQueueMessage(`Queued ${shortId(job.id)}. Worker will process it in the background.`);
      await refreshJobs();
      await refreshHistoricalData();
    } catch (error) {
      if (error instanceof CivicApiError && error.status === 401) {
        expireSession("Your operator session expired. Sign in again.");
        setQueueState("error");
        setQueueMessage("Session expired. Sign in again before queueing an import job.");
        return;
      }

      setQueueState("error");
      setQueueMessage(error instanceof CivicApiError ? error.message : "Could not queue import job.");
    }
  }

  async function retryImportJob(jobId: string) {
    const routesAvailable = backendReady || (await verifyBackendRoutes());
    if (!routesAvailable) {
      setQueueState("error");
      setQueueMessage("Data Sources backend routes are unavailable.");
      return;
    }

    setRetryingJobId(jobId);
    setQueueState("loading");
    setQueueMessage(`Retrying ${shortId(jobId)}...`);

    try {
      const retried = await retryDataImportJob(jobId, accessToken || undefined);
      setJobs((currentJobs) => currentJobs.map((job) => (job.id === retried.id ? retried : job)));
      setQueueState("ready");
      setQueueMessage(`Retry queued for ${shortId(jobId)}. Worker will pick it up shortly.`);
      await refreshJobs();
    } catch (error) {
      if (error instanceof CivicApiError && error.status === 401) {
        expireSession("Your operator session expired. Sign in again.");
        setQueueState("error");
        setQueueMessage("Session expired. Sign in again before retrying an import job.");
        return;
      }

      setQueueState("error");
      setQueueMessage(error instanceof CivicApiError ? error.message : "Could not retry import job.");
    } finally {
      setRetryingJobId(null);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        actions={
          <button
            className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface px-3 text-sm font-semibold text-civic-primary hover:bg-white"
            onClick={() => {
              void (async () => {
                const routesAvailable = await verifyBackendRoutes();

                if (routesAvailable) {
                  void refreshJobs();
                  void refreshHistoricalData();
                }
              })();
            }}
            type="button"
          >
            <RefreshCw className="h-4 w-4" aria-hidden="true" />
            Refresh
          </button>
        }
        description="Operate public data ingestion and monitor background import jobs."
        eyebrow="Administration"
        title="Data Sources"
      />

      {backendState !== "ready" ? (
        <div className="rounded-lg border border-status-critical/30 bg-status-critical/10 p-4 text-sm leading-6 text-status-critical-text">
          <div className="flex items-start gap-3">
            {backendState === "loading" ? <Loader2 className="mt-0.5 h-5 w-5 animate-spin" aria-hidden="true" /> : <TriangleAlert className="mt-0.5 h-5 w-5" aria-hidden="true" />}
            <div>
              <p className="font-semibold">{backendState === "loading" ? "Checking backend" : "Backend route mismatch"}</p>
              <p>{backendMessage}</p>
              {backendState === "error" ? <p className="mt-2 font-semibold">Run: docker compose up --build</p> : null}
            </div>
          </div>
        </div>
      ) : null}

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard icon={<Database className="h-5 w-5" />} label="Historical 311 records" trend={lastRefreshedAt ? `Synced ${formatTime(lastRefreshedAt)}` : "Available to map/search APIs"} value={String(summary?.totalCount ?? 0)} />
        <MetricCard icon={<DownloadCloud className="h-5 w-5" />} label="Import jobs" trend={`${runningCount} pending or running`} value={String(jobs.length)} />
        <MetricCard icon={<History className="h-5 w-5" />} label="Records processed" tone="calm" trend="Across visible job history" value={String(processedRecords)} />
        <MetricCard icon={<ShieldCheck className="h-5 w-5" />} label="Latest status" tone={latestJob?.status === "Failed" ? "alert" : "review"} trend={latestJob ? formatDateTime(latestJob.updatedAt) : "No jobs yet"} value={latestJob?.status ?? "Idle"} />
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,0.9fr)_minmax(420px,1.1fr)]">
        <div className="grid gap-6">
          <Panel title="Operator Access" description={authMessage}>
            <div className="grid gap-3">
              <label className="grid gap-2 text-sm font-semibold text-civic-heading">
                Email
                <input
                  autoComplete="email"
                  className={fieldClassName}
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="operator@civicsignal.local"
                  value={email}
                />
              </label>
              <label className="grid gap-2 text-sm font-semibold text-civic-heading">
                Password
                <input
                  autoComplete="current-password"
                  className={fieldClassName}
                  onChange={(event) => setPassword(event.target.value)}
                  placeholder="Password"
                  type="password"
                  value={password}
                />
              </label>
              <div className="grid gap-2 sm:grid-cols-2">
                <button
                  className="inline-flex h-11 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-sm font-semibold text-white hover:bg-civic-primary-strong disabled:opacity-60"
                  disabled={authState === "loading" || !email.trim() || !password}
                  onClick={() => void signIn()}
                  type="button"
                >
                  {authState === "loading" ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <KeyRound className="h-4 w-4" aria-hidden="true" />}
                  Sign In
                </button>
                <button
                  className="inline-flex h-11 items-center justify-center gap-2 rounded-md border border-civic-border px-4 text-sm font-semibold text-civic-primary hover:bg-civic-soft"
                  onClick={clearToken}
                  type="button"
                >
                  Clear Token
                </button>
              </div>
            </div>
          </Panel>

          <Panel title="NYC 311 Import" description={queueMessage}>
            <div className="grid gap-4">
              <div className="grid gap-3 sm:grid-cols-2">
                <label className="grid gap-2 text-sm font-semibold text-civic-heading">
                  Days back
                  <input className={fieldClassName} max={3650} min={1} onChange={(event) => setDaysBack(Number(event.target.value))} type="number" value={daysBack} />
                </label>
                <label className="grid gap-2 text-sm font-semibold text-civic-heading">
                  Limit
                  <input className={fieldClassName} max={5000} min={1} onChange={(event) => setLimit(Number(event.target.value))} type="number" value={limit} />
                </label>
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <label className="grid gap-2 text-sm font-semibold text-civic-heading">
                  Borough
                  <select className={fieldClassName} onChange={(event) => setBorough(event.target.value)} value={borough}>
                    {["All", "MANHATTAN", "BROOKLYN", "QUEENS", "BRONX", "STATEN ISLAND"].map((item) => (
                      <option key={item}>{item}</option>
                    ))}
                  </select>
                </label>
                <label className="grid gap-2 text-sm font-semibold text-civic-heading">
                  Complaint type
                  <input className={fieldClassName} onChange={(event) => setComplaintType(event.target.value)} placeholder="Street Condition" value={complaintType} />
                </label>
              </div>
              <button
                className="inline-flex h-12 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-sm font-semibold text-white hover:bg-civic-primary-strong disabled:opacity-60"
                disabled={queueState === "loading"}
                onClick={() => void queueImport()}
                type="button"
              >
                {queueState === "loading" ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <Play className="h-4 w-4" aria-hidden="true" />}
                Queue Import Job
              </button>
            </div>
          </Panel>

          <LiveImportMonitor job={latestJob} lastRefreshedAt={lastRefreshedAt} runningCount={runningCount} />
        </div>

        <div className="grid gap-6">
          <Panel
            action={<SegmentedControl onChange={setStatus} options={statusFilters} value={status} />}
            title="Import History"
            description={`${filteredJobs.length} visible job${filteredJobs.length === 1 ? "" : "s"}.`}
          >
            <div className="grid gap-3">
              {filteredJobs.length ? (
                filteredJobs.map((job) => (
                  <ImportJobRow
                    isRetrying={retryingJobId === job.id}
                    job={job}
                    key={job.id}
                    onRetry={(jobId) => void retryImportJob(jobId)}
                  />
                ))
              ) : (
                <div className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm font-semibold text-civic-muted">
                  {jobsState === "loading" ? "Loading job history..." : "No import jobs match this view."}
                </div>
              )}
            </div>
          </Panel>

          <Panel title="Historical Complaint Mix" description={summary ? `Newest record ${formatDateTime(summary.newestCreatedAt)}` : "Summary API standby."}>
            <div className="grid gap-3">
              {(summary?.topCategories ?? []).slice(0, 5).map((bucket) => (
                <div className="rounded-md border border-civic-border bg-civic-raised p-3" key={bucket.value}>
                  <div className="mb-2 flex items-center justify-between gap-3 text-sm">
                    <span className="font-semibold text-civic-heading">{bucket.value}</span>
                    <span className="text-civic-muted">{bucket.count}</span>
                  </div>
                  <ScoreBar score={summary?.totalCount ? (bucket.count / summary.totalCount) * 100 : 0} />
                </div>
              ))}
              {summary && summary.topCategories.length === 0 ? (
                <div className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm font-semibold text-civic-muted">
                  Historical complaint records will appear after an import job succeeds.
                </div>
              ) : null}
            </div>
          </Panel>

          <Panel title="Recent Imported Records" description="Newest historical 311 complaints available to map and analytics views.">
            <div className="grid gap-3">
              {recentComplaints.length ? (
                recentComplaints.map((complaint) => <RecentComplaintRow complaint={complaint} key={complaint.id} />)
              ) : (
                <div className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm font-semibold text-civic-muted">
                  No historical records imported yet.
                </div>
              )}
            </div>
          </Panel>
        </div>
      </div>
    </div>
  );
}

function ImportJobRow({
  isRetrying,
  job,
  onRetry,
}: {
  isRetrying: boolean;
  job: DataImportJobDto;
  onRetry: (jobId: string) => void;
}) {
  const parameters = parseParameters(job.parametersJson);

  return (
    <div className="rounded-md border border-civic-border bg-civic-raised p-4">
      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-semibold text-civic-heading">{shortId(job.id)}</span>
            <StatusPill status={job.status} />
          </div>
          <p className="mt-2 text-sm leading-6 text-civic-muted">
            {job.source} - {job.importType} - requested {formatDateTime(job.requestedAt)}
          </p>
          <p className="mt-1 text-xs font-semibold text-civic-muted">
            {job.startedAt ? `Started ${formatTime(job.startedAt)}` : "Waiting for worker"} - {job.finishedAt ? `finished ${formatTime(job.finishedAt)}` : "not finished"}
          </p>
        </div>
        <div className="grid gap-3">
          <div className="grid grid-cols-4 gap-2 text-center text-xs font-semibold text-civic-muted">
            <MiniCount label="Received" value={job.receivedCount} />
            <MiniCount label="Created" value={job.createdCount} />
            <MiniCount label="Updated" value={job.updatedCount} />
            <MiniCount label="Skipped" value={job.skippedCount} />
          </div>
          {job.status === "Failed" ? (
            <button
              className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-status-critical/30 bg-white px-3 text-sm font-semibold text-status-critical-text hover:bg-status-critical/10 disabled:opacity-60"
              disabled={isRetrying}
              onClick={() => onRetry(job.id)}
              type="button"
            >
              {isRetrying ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <RotateCcw className="h-4 w-4" aria-hidden="true" />}
              Retry
            </button>
          ) : null}
        </div>
      </div>
      <div className="mt-4">
        <ScoreBar label={jobProgressLabel(job.status)} score={jobProgress(job.status)} />
      </div>
      <div className="mt-3 flex flex-wrap gap-2 text-xs font-semibold text-civic-muted">
        <span className="rounded-md bg-civic-surface px-2 py-1">limit {parameters.limit ?? "default"}</span>
        <span className="rounded-md bg-civic-surface px-2 py-1">days {parameters.daysBack ?? "default"}</span>
        <span className="rounded-md bg-civic-surface px-2 py-1">borough {parameters.borough ?? "all"}</span>
        {parameters.complaintType ? <span className="rounded-md bg-civic-surface px-2 py-1">{parameters.complaintType}</span> : null}
      </div>
      {job.errorMessage ? <p className="mt-3 rounded-md bg-status-critical/10 p-3 text-sm font-semibold text-status-critical-text">{job.errorMessage}</p> : null}
    </div>
  );
}

function MiniCount({ label, value }: { label: string; value: number }) {
  return (
    <span className="rounded-md bg-civic-surface px-2 py-1.5">
      <span className="block text-civic-heading">{value}</span>
      <span>{label}</span>
    </span>
  );
}

function LiveImportMonitor({
  job,
  lastRefreshedAt,
  runningCount,
}: {
  job?: DataImportJobDto;
  lastRefreshedAt: string | null;
  runningCount: number;
}) {
  return (
    <Panel
      title="Live Import Monitor"
      description={job ? `${shortId(job.id)} updates every ${runningCount > 0 ? "3" : "10"} seconds.` : "Queue an import job to start monitoring."}
    >
      <div className="grid gap-4">
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-civic-border bg-civic-raised p-4">
          <div className="flex items-center gap-3">
            <span className={`flex h-10 w-10 items-center justify-center rounded-md ${runningCount > 0 ? "bg-status-review text-status-review-text" : "bg-civic-soft text-civic-primary"}`}>
              {runningCount > 0 ? <Radio className="h-5 w-5 animate-pulse" aria-hidden="true" /> : <Clock3 className="h-5 w-5" aria-hidden="true" />}
            </span>
            <div>
              <p className="font-semibold text-civic-heading">{runningCount > 0 ? "Import worker active" : "Monitor standing by"}</p>
              <p className="text-sm text-civic-muted">{lastRefreshedAt ? `Last refresh ${formatTime(lastRefreshedAt)}` : "Waiting for first refresh"}</p>
            </div>
          </div>
          {job ? <StatusPill status={job.status} /> : null}
        </div>

        <div className="grid gap-3">
          <PipelineStep done={Boolean(job)} label="Requested" value={job ? formatTime(job.requestedAt) : "Not queued"} />
          <PipelineStep active={job?.status === "Pending"} done={Boolean(job?.startedAt || job?.status === "Running" || job?.status === "Succeeded" || job?.status === "Failed")} label="Queued" value={job?.status === "Pending" ? "Waiting for worker" : job ? "Accepted" : "Not queued"} />
          <PipelineStep active={job?.status === "Running"} done={job?.status === "Succeeded" || job?.status === "Failed"} label="Processing" value={job?.startedAt ? formatTime(job.startedAt) : "Not started"} />
          <PipelineStep alert={job?.status === "Failed"} done={job?.status === "Succeeded"} label={job?.status === "Failed" ? "Failed" : "Completed"} value={job?.finishedAt ? formatTime(job.finishedAt) : "Not finished"} />
        </div>
      </div>
    </Panel>
  );
}

function PipelineStep({
  active = false,
  alert = false,
  done = false,
  label,
  value,
}: {
  active?: boolean;
  alert?: boolean;
  done?: boolean;
  label: string;
  value: string;
}) {
  const iconClass = alert
    ? "bg-status-critical text-status-critical-text"
    : done
      ? "bg-status-approved text-status-approved-text"
      : active
        ? "bg-status-review text-status-review-text"
        : "bg-civic-surface text-civic-muted";

  return (
    <div className="grid grid-cols-[2.5rem_minmax(0,1fr)] gap-3 rounded-md border border-civic-border bg-civic-raised p-3">
      <span className={`flex h-10 w-10 items-center justify-center rounded-md ${iconClass}`}>
        {alert ? <TriangleAlert className="h-4 w-4" aria-hidden="true" /> : active ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : done ? <CheckCircle2 className="h-4 w-4" aria-hidden="true" /> : <Clock3 className="h-4 w-4" aria-hidden="true" />}
      </span>
      <span className="min-w-0">
        <span className="block font-semibold text-civic-heading">{label}</span>
        <span className="block truncate text-sm text-civic-muted">{value}</span>
      </span>
    </div>
  );
}

function RecentComplaintRow({ complaint }: { complaint: HistoricalComplaintDto }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-raised p-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0">
          <p className="truncate font-semibold text-civic-heading">{complaint.complaintType}</p>
          <p className="mt-1 text-sm leading-6 text-civic-muted">{complaint.category} - {complaint.agency ?? "Agency pending"} - {formatTime(complaint.createdAt)}</p>
        </div>
        <span className="inline-flex items-center gap-1 rounded-md bg-civic-soft px-2 py-1 text-xs font-semibold text-civic-primary">
          <MapPin className="h-3.5 w-3.5" aria-hidden="true" />
          {complaint.borough ?? "NYC"}
        </span>
      </div>
      <p className="mt-3 text-xs font-semibold text-civic-muted">
        {complaint.latitude.toFixed(4)}, {complaint.longitude.toFixed(4)}
      </p>
    </div>
  );
}

function StatusPill({ status }: { status: string }) {
  const icon =
    status === "Succeeded" ? (
      <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" />
    ) : status === "Failed" ? (
      <TriangleAlert className="h-3.5 w-3.5" aria-hidden="true" />
    ) : status === "Running" ? (
      <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" />
    ) : (
      <Clock3 className="h-3.5 w-3.5" aria-hidden="true" />
    );
  const className =
    status === "Succeeded"
      ? "bg-status-approved text-status-approved-text"
      : status === "Failed"
        ? "bg-status-critical text-status-critical-text"
        : status === "Running"
          ? "bg-status-review text-status-review-text"
          : "bg-status-submitted text-status-submitted-text";

  return (
    <span className={`inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-semibold ${className}`}>
      {icon}
      {status}
    </span>
  );
}

function jobProgress(status: string) {
  if (status === "Succeeded") {
    return 100;
  }

  if (status === "Failed") {
    return 100;
  }

  if (status === "Running") {
    return 66;
  }

  if (status === "Pending") {
    return 33;
  }

  return 0;
}

function jobProgressLabel(status: string) {
  if (status === "Succeeded") {
    return "Import completed";
  }

  if (status === "Failed") {
    return "Import failed";
  }

  if (status === "Running") {
    return "Import running";
  }

  if (status === "Pending") {
    return "Import queued";
  }

  return "Import status";
}

function parseParameters(value: string) {
  try {
    return JSON.parse(value) as {
      borough?: string | null;
      complaintType?: string | null;
      daysBack?: number | null;
      limit?: number | null;
    };
  } catch {
    return {};
  }
}

function shortId(value: string) {
  return `${value.slice(0, 8)}...${value.slice(-6)}`;
}

function formatDateTime(value?: string | null) {
  if (!value) {
    return "not available";
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

function formatTime(value?: string | null) {
  if (!value) {
    return "not available";
  }

  return new Intl.DateTimeFormat(undefined, {
    hour: "numeric",
    minute: "2-digit",
    second: "2-digit",
  }).format(new Date(value));
}

function normalizeRoute(route: string) {
  return route.replace(/^\/+/, "").trim();
}

function readStoredAccessToken() {
  if (typeof window === "undefined") {
    return null;
  }

  const token = window.localStorage.getItem(tokenStorageKey);
  const expiresAt = Number(window.localStorage.getItem(tokenExpiresAtStorageKey));

  if (!token || !Number.isFinite(expiresAt) || expiresAt <= Date.now() + tokenRefreshSkewMs) {
    clearStoredAccessToken();
    return null;
  }

  return token;
}

function storeAccessToken(token: string, expiresInSeconds: number) {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(tokenStorageKey, token);
  window.localStorage.setItem(tokenExpiresAtStorageKey, String(Date.now() + Math.max(60, expiresInSeconds) * 1000));
}

function clearStoredAccessToken() {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.removeItem(tokenStorageKey);
  window.localStorage.removeItem(tokenExpiresAtStorageKey);
}

function buildBackendReadinessMessage(error: unknown) {
  if (error instanceof CivicApiError && error.status === 404) {
    return `The API at ${apiBaseUrl} is reachable, but it does not expose /api/system/capabilities. Rebuild and restart the backend so the latest Data Sources controllers are running.`;
  }

  if (error instanceof CivicApiError) {
    return error.message;
  }

  return `The API at ${apiBaseUrl} is not reachable from the browser session. Start or restart the backend.`;
}
