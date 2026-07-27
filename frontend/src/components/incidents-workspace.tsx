"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useCallback, useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  ClipboardCheck,
  Clock3,
  Database,
  Filter,
  GitMerge,
  History,
  Layers,
  LocateFixed,
  MapPin,
  MessageSquareText,
  RadioTower,
  RefreshCw,
  Route,
  Search,
  Send,
  ShieldCheck,
  SlidersHorizontal,
  Target,
  UserCheck,
} from "lucide-react";
import { statusLabel } from "@/lib/civic-analysis";
import {
  assignIncident,
  CivicApiError,
  dispatchIncident,
  getDuplicateCandidates,
  getHistoricalComplaintSummary,
  getLatestPrediction,
  isIncidentId,
  markIncidentDuplicate,
  runControlledAgentWorkflow,
  searchHistoricalComplaints,
  searchIncidents,
  type ControlledTriageWorkflowDto,
  type DuplicateCandidateDto,
  type HistoricalComplaintDto,
  type HistoricalComplaintSummaryDto,
  type IncidentDto,
  type ProcessingStepDto,
  type TriagePredictionDto,
} from "@/lib/civic-api";
import {
  createIncidentHubConnection,
  incidentRealtimeEvents,
  incidentRealtimeEventTypes,
  isIncidentRealtimeAvailable,
  type IncidentRealtimeEventDto,
  type RealtimeConnectionState,
} from "@/lib/civic-realtime";
import { readStoredAdminAccessToken } from "@/lib/admin-auth";
import { AgentWorkflowPanel, type AgentWorkflowRunState } from "@/components/agent-workflow-panel";
import { IncidentOperationsMap } from "@/components/incident-operations-map";
import type { IncidentRow, IncidentStatus, Severity } from "@/lib/civic-types";
import { fieldClassName, MetricCard, PageHeader, Panel, ScoreBar, SegmentedControl, SeverityBadge, StatusBadge } from "@/components/ui-kit";

const statusFilters = ["All", "Submitted", "Triaged", "HumanReviewRequired", "Approved", "Dispatched"] as const;
const severityFilters = ["All", "Low", "Medium", "High", "Critical"] as const;
const detailTabs = ["Details", "Agent", "Similar", "Timeline"] as const;

type LoadState = "loading" | "live" | "empty" | "error";
type HistoricalLoadState = "idle" | "loading" | "live" | "empty" | "error";
type FilterValue = string;
type AgentRun = {
  message?: string;
  state: AgentWorkflowRunState;
  workflow?: ControlledTriageWorkflowDto;
};
type IncidentEnrichment = {
  duplicateCandidates: DuplicateCandidateDto[];
  prediction: TriagePredictionDto | null;
};

export function IncidentsWorkspace() {
  const [rows, setRows] = useState<IncidentRow[]>([]);
  const [historicalComplaints, setHistoricalComplaints] = useState<HistoricalComplaintDto[]>([]);
  const [historicalSummary, setHistoricalSummary] = useState<HistoricalComplaintSummaryDto | null>(null);
  const [historicalState, setHistoricalState] = useState<HistoricalLoadState>("idle");
  const [loadState, setLoadState] = useState<LoadState>("loading");
  const [loadMessage, setLoadMessage] = useState("Loading live incident data from the API.");
  const [realtimeState, setRealtimeState] = useState<RealtimeConnectionState>("idle");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<(typeof statusFilters)[number]>("All");
  const [severity, setSeverity] = useState<(typeof severityFilters)[number]>("All");
  const [agency, setAgency] = useState<FilterValue>("All");
  const [category, setCategory] = useState<FilterValue>("All");
  const [zone, setZone] = useState<FilterValue>("All");
  const [selectedId, setSelectedId] = useState("");
  const [detailTab, setDetailTab] = useState<(typeof detailTabs)[number]>("Details");
  const [caseAction, setCaseAction] = useState("No case action selected");
  const [caseActionBusy, setCaseActionBusy] = useState<"assign" | "dispatch" | "merge" | null>(null);
  const [agentRuns, setAgentRuns] = useState<Record<string, AgentRun>>({});

  const loadIncidents = useCallback(async () => {
    const accessToken = readStoredAdminAccessToken() ?? undefined;

    setLoadState("loading");
    setLoadMessage("Loading incidents from the CivicSignal API...");

    try {
      const apiIncidents = await searchIncidents({ pageSize: 100 }, accessToken);

      if (apiIncidents.length === 0) {
        setRows([]);
        setSelectedId("");
        setRealtimeState("idle");
        setLoadState("empty");
        setLoadMessage("No backend incidents are available yet. Submit a citizen report or seed backend data, then refresh.");
        return;
      }

      const enrichmentPairs = await Promise.all(
        apiIncidents.map(async (incident) => {
          const [duplicateCandidates, prediction] = await Promise.all([
            getDuplicateCandidates(incident.id, accessToken).catch(() => [] as DuplicateCandidateDto[]),
            getLatestPrediction(incident.id, accessToken).catch(() => null),
          ]);

          return [
            incident.id,
            {
              duplicateCandidates,
              prediction,
            },
          ] as const;
        }),
      );
      const enrichmentMap = new Map<string, IncidentEnrichment>(enrichmentPairs);
      const mappedRows = apiIncidents.map((incident) => {
        const enrichment = enrichmentMap.get(incident.id);

        return mapBackendIncident(incident, enrichment?.duplicateCandidates ?? [], enrichment?.prediction ?? null);
      });
      setRows(mappedRows);
      setSelectedId((current) => (mappedRows.some((incident) => incident.id === current) ? current : mappedRows[0].id));
      setRealtimeState("connecting");
      setLoadState("live");
      setLoadMessage(`${mappedRows.length} incidents loaded from the backend API.`);
    } catch (error) {
      setRows([]);
      setSelectedId("");
      setRealtimeState("offline");
      setLoadState("error");
      setLoadMessage(error instanceof CivicApiError ? error.message : "API unavailable. Live incidents could not be loaded.");
    }
  }, []);

  const loadHistoricalContext = useCallback(async () => {
    setHistoricalState("loading");

    try {
      const options = {
        agency: agency === "All" ? undefined : agency,
        category: category === "All" ? undefined : category,
        pageSize: 300,
        query: search.trim() || undefined,
      };
      const [complaints, summary] = await Promise.all([
        searchHistoricalComplaints(options),
        getHistoricalComplaintSummary(options),
      ]);

      setHistoricalComplaints(complaints);
      setHistoricalSummary(summary);
      setHistoricalState(complaints.length ? "live" : "empty");
    } catch {
      setHistoricalComplaints([]);
      setHistoricalSummary(null);
      setHistoricalState("error");
    }
  }, [agency, category, search]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadIncidents();
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadIncidents]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadHistoricalContext();
    }, 250);

    return () => window.clearTimeout(timer);
  }, [loadHistoricalContext]);

  useEffect(() => {
    if (loadState !== "live") {
      return;
    }

    let active = true;
    let connection: ReturnType<typeof createIncidentHubConnection> | null = null;

    const applyRealtimeEvent = (event: IncidentRealtimeEventDto) => {
      if (!active) {
        return;
      }

      setRows((currentRows) => applyRealtimeEventToRows(currentRows, event));
      setSelectedId((current) => current || event.incidentId);
      setLoadState("live");
      setLoadMessage(`Live update ${formatRealtimeTime(event.occurredAt)}: ${event.message}`);

      if (!event.incident || event.eventType === incidentRealtimeEventTypes.analyzed) {
        window.setTimeout(() => {
          if (active) {
            void loadIncidents();
          }
        }, 300);
      }
    };

    async function startRealtime() {
      const available = await isIncidentRealtimeAvailable();

      if (!active) {
        return;
      }

      if (!available) {
        setRealtimeState("offline");
        return;
      }

      const accessToken = readStoredAdminAccessToken() ?? undefined;

      connection = createIncidentHubConnection(accessToken);
      connection.on(incidentRealtimeEvents.operationsIncidentUpdated, applyRealtimeEvent);
      connection.onreconnecting(() => {
        if (active) {
          setRealtimeState("reconnecting");
        }
      });
      connection.onreconnected(async () => {
        if (!active) {
          return;
        }

        await connection?.invoke("SubscribeToOperations");
        setRealtimeState("connected");
      });
      connection.onclose(() => {
        if (active) {
          setRealtimeState("offline");
        }
      });

      try {
        await connection.start();

        if (!active) {
          return;
        }

        await connection.invoke("SubscribeToOperations");
        setRealtimeState("connected");
      } catch {
        if (active) {
          setRealtimeState("offline");
        }
      }
    }

    void startRealtime();

    return () => {
      active = false;
      connection?.off(incidentRealtimeEvents.operationsIncidentUpdated, applyRealtimeEvent);
      void connection?.stop();
    };
  }, [loadIncidents, loadState]);

  const agencies = useMemo(() => uniqueSorted(rows.map((incident) => incident.agency)), [rows]);
  const categories = useMemo(() => uniqueSorted(rows.map((incident) => incident.category)), [rows]);
  const zones = useMemo(() => uniqueSorted(rows.map((incident) => String(incident.cityZone))), [rows]);

  const filteredIncidents = useMemo(() => {
    const term = search.trim().toLowerCase();

    return rows.filter((incident) => {
      const matchesSearch =
        !term ||
        incident.id.toLowerCase().includes(term) ||
        incident.title.toLowerCase().includes(term) ||
        incident.location.toLowerCase().includes(term) ||
        incident.category.toLowerCase().includes(term) ||
        incident.agency.toLowerCase().includes(term);
      const matchesStatus = status === "All" || incident.status === status;
      const matchesSeverity = severity === "All" || incident.severity === severity;
      const matchesAgency = agency === "All" || incident.agency === agency;
      const matchesCategory = category === "All" || incident.category === category;
      const matchesZone = zone === "All" || String(incident.cityZone) === zone;

      return matchesSearch && matchesStatus && matchesSeverity && matchesAgency && matchesCategory && matchesZone;
    });
  }, [agency, category, rows, search, severity, status, zone]);

  const selectedIncident = rows.find((incident) => incident.id === selectedId) ?? filteredIncidents[0] ?? rows[0];
  const hotZones = useMemo(() => getHotZones(filteredIncidents), [filteredIncidents]);
  const duplicateSignals = useMemo(
    () => filteredIncidents.filter((incident) => incident.duplicates.length > 0).sort((left, right) => right.duplicates.length - left.duplicates.length),
    [filteredIncidents],
  );
  const reviewCount = filteredIncidents.filter((incident) => incident.status === "HumanReviewRequired").length;
  const duplicateCount = filteredIncidents.reduce((total, incident) => total + incident.duplicates.length, 0);
  const nearbyHistoricalComplaints = useMemo(
    () => (selectedIncident ? getNearbyHistoricalComplaints(selectedIncident, historicalComplaints, 5) : []),
    [historicalComplaints, selectedIncident],
  );
  const nearbyHistoricalCount = nearbyHistoricalComplaints.filter((item) => item.distanceMeters <= 500).length;
  const nearestHistoricalComplaint = nearbyHistoricalComplaints[0];
  const selectedAgentRun = selectedIncident ? agentRuns[selectedIncident.id] : undefined;

  function resetFilters() {
    setSearch("");
    setStatus("All");
    setSeverity("All");
    setAgency("All");
    setCategory("All");
    setZone("All");
  }

  async function runSelectedAgentWorkflow() {
    if (!selectedIncident) {
      setCaseAction("Select a live backend incident before running the controlled workflow.");
      setDetailTab("Agent");
      return;
    }

    const incidentId = selectedIncident.id;
    if (!isIncidentId(incidentId)) {
      setAgentRuns((current) => ({
        ...current,
        [incidentId]: {
          message: "The controlled workflow runs only on live backend incidents.",
          state: "error",
        },
      }));
      setDetailTab("Agent");
      return;
    }

    const accessToken = readStoredAdminAccessToken() ?? undefined;

    setDetailTab("Agent");
    setAgentRuns((current) => ({
      ...current,
      [incidentId]: {
        ...current[incidentId],
        message: "Running controlled triage workflow...",
        state: "running",
      },
    }));

    try {
      const workflow = await runControlledAgentWorkflow(incidentId, accessToken);
      const nextMessage = workflow.requiresHumanReview
        ? `Workflow completed with reviewer gate: ${workflow.reviewReason ?? "review required"}.`
        : `Workflow prepared a draft work order for ${workflow.draftWorkOrder?.agencyCode ?? selectedIncident.agency}.`;

      setAgentRuns((current) => ({
        ...current,
        [incidentId]: {
          message: nextMessage,
          state: "ready",
          workflow,
        },
      }));
      setCaseAction(`${formatCaseId(incidentId)} agent workflow completed`);
      void loadIncidents();
    } catch (error) {
      const errorMessage = error instanceof CivicApiError ? error.message : "Could not run the controlled workflow.";

      setAgentRuns((current) => ({
        ...current,
        [incidentId]: {
          ...current[incidentId],
          message: errorMessage,
          state: "error",
        },
      }));
      setCaseAction(`${formatCaseId(incidentId)} agent workflow failed`);
    }
  }

  function replaceIncidentRow(incident: IncidentDto) {
    const nextRow = mapBackendIncident(incident);
    setRows((currentRows) =>
      currentRows.map((row) =>
        row.id === nextRow.id
          ? {
              ...row,
              ...nextRow,
              agency: incident.assignedAgencyCode ?? incident.correctedAgencyCode ?? row.agency,
              aiSummary: row.aiSummary,
              category: incident.correctedCategory ?? row.category,
              confidence: row.confidence,
              duplicates: nextRow.duplicates.length ? nextRow.duplicates : row.duplicates,
              evidence: row.evidence,
              severity: normalizeSeverity(incident.correctedSeverity ?? row.severity),
              slaRisk: calculateSlaRisk(nextRow.status, normalizeSeverity(incident.correctedSeverity ?? row.severity), row.confidence),
              title: `${statusLabel(incident.correctedCategory ?? row.category)} report`,
            }
          : row,
      ),
    );
    setSelectedId(nextRow.id);
    return nextRow;
  }

  async function assignSelectedIncident() {
    if (!selectedIncident) {
      setCaseAction("Select a live backend incident before assignment.");
      return;
    }

    const accessToken = readStoredAdminAccessToken() ?? undefined;
    if (!accessToken) {
      setCaseAction("Operator session required. Sign in again to assign incidents.");
      return;
    }

    setCaseActionBusy("assign");
    setCaseAction(`Assigning ${formatCaseId(selectedIncident.id)}...`);

    try {
      const updated = await assignIncident(
        selectedIncident.id,
        {
          assignedAgencyCode: selectedIncident.agency,
          assignedTeam: selectedIncident.assignedTeam,
          note: "Assigned from the operations console.",
        },
        accessToken,
      );
      const nextRow = replaceIncidentRow(updated);
      setCaseAction(`${formatCaseId(nextRow.id)} assigned to ${nextRow.assignedTeam}.`);
    } catch (error) {
      setCaseAction(error instanceof CivicApiError ? error.message : "Assignment failed.");
    } finally {
      setCaseActionBusy(null);
    }
  }

  async function dispatchSelectedIncident() {
    if (!selectedIncident) {
      setCaseAction("Select a live backend incident before dispatch.");
      return;
    }

    const accessToken = readStoredAdminAccessToken() ?? undefined;
    if (!accessToken) {
      setCaseAction("Operator session required. Sign in again to dispatch incidents.");
      return;
    }

    setCaseActionBusy("dispatch");
    setCaseAction(`Dispatching ${formatCaseId(selectedIncident.id)}...`);

    try {
      const updated = await dispatchIncident(
        selectedIncident.id,
        { note: `Dispatched to ${selectedIncident.assignedTeam}.` },
        accessToken,
      );
      const nextRow = replaceIncidentRow(updated);
      setCaseAction(`${formatCaseId(nextRow.id)} dispatched to field operations.`);
    } catch (error) {
      setCaseAction(error instanceof CivicApiError ? error.message : "Dispatch failed.");
    } finally {
      setCaseActionBusy(null);
    }
  }

  async function markSelectedIncidentDuplicate() {
    if (!selectedIncident) {
      setCaseAction("Select a live backend incident before linking a duplicate.");
      return;
    }

    const duplicate = selectedIncident.duplicates[0];
    if (!duplicate) {
      setDetailTab("Similar");
      setCaseAction("No duplicate candidate is available for this incident yet. Run the agent workflow or wait for duplicate detection.");
      return;
    }

    const accessToken = readStoredAdminAccessToken() ?? undefined;
    if (!accessToken) {
      setCaseAction("Operator session required. Sign in again to link duplicate incidents.");
      return;
    }

    setCaseActionBusy("merge");
    setCaseAction(`Linking ${formatCaseId(selectedIncident.id)} to ${formatCaseId(duplicate.caseId)}...`);

    try {
      const updated = await markIncidentDuplicate(
        selectedIncident.id,
        {
          duplicateOfIncidentId: duplicate.caseId,
          note: `Marked duplicate of ${duplicate.caseId} from the operations console.`,
        },
        accessToken,
      );
      const nextRow = replaceIncidentRow(updated);
      setDetailTab("Similar");
      setCaseAction(`${formatCaseId(nextRow.id)} linked as duplicate of ${formatCaseId(duplicate.caseId)}.`);
    } catch (error) {
      setCaseAction(error instanceof CivicApiError ? error.message : "Duplicate link failed.");
    } finally {
      setCaseActionBusy(null);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        actions={
          <button
            className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface px-3 text-sm font-semibold text-civic-primary hover:bg-white"
            onClick={() => void loadIncidents()}
            type="button"
          >
            <RefreshCw className="h-4 w-4" aria-hidden="true" />
            Refresh
          </button>
        }
        description="Map, filter, and inspect incidents without exposing the frontend to PostgreSQL, storage, queues, or AI models directly."
        eyebrow="Operations"
        title="Incident Map"
      />

      <div
        className={`flex flex-col gap-3 rounded-lg border p-4 sm:flex-row sm:items-center sm:justify-between ${
          loadState === "error"
            ? "border-status-review bg-status-review/20"
            : loadState === "live"
              ? "border-status-approved bg-status-approved/20"
              : "border-civic-border bg-civic-surface"
        }`}
      >
        <div className="flex items-center gap-3">
          <span className="rounded-md bg-civic-soft p-2 text-civic-primary">
            <RadioTower className="h-4 w-4" aria-hidden="true" />
          </span>
          <span>
            <span className="block text-sm font-semibold text-civic-heading">{loadSourceLabel(loadState)}</span>
            <span className="mt-1 block text-sm text-civic-muted">{loadMessage}</span>
          </span>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <span className="rounded-md bg-civic-soft px-3 py-2 text-sm font-semibold text-civic-primary">
            {filteredIncidents.length} visible of {rows.length}
          </span>
          <span className={`rounded-md border px-3 py-2 text-sm font-semibold ${realtimeClassName(realtimeState)}`}>
            {realtimeLabel(realtimeState)}
          </span>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard icon={<Layers className="h-5 w-5" />} label="Visible incidents" trend="After current filters" value={String(filteredIncidents.length)} />
        <MetricCard icon={<ShieldCheck className="h-5 w-5" />} label="Needs review" tone="review" trend="Human-in-loop cases" value={String(reviewCount)} />
        <MetricCard icon={<GitMerge className="h-5 w-5" />} label="Duplicate signals" tone="calm" trend="Similar or nearby reports" value={String(duplicateCount)} />
        <MetricCard
          icon={<AlertTriangle className="h-5 w-5" />}
          label="311 context"
          tone="alert"
          trend={historicalStateLabel(historicalState)}
          value={String(historicalSummary?.totalCount ?? historicalComplaints.length)}
        />
      </div>

      <Panel title="Filters" description="Narrow the operations map by status, severity, agency, category, and city zone.">
        <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_auto]">
          <label className="relative block">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-civic-muted" aria-hidden="true" />
            <input
              className={`${fieldClassName} pl-10`}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search by id, location, category, agency, or title"
              value={search}
            />
          </label>
          <SegmentedControl onChange={setStatus} options={statusFilters} value={status} />
        </div>

        <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
          <FilterSelect
            icon={<SlidersHorizontal className="h-4 w-4" />}
            label="Severity"
            onChange={(value) => setSeverity(value as (typeof severityFilters)[number])}
            options={severityFilters}
            value={severity}
          />
          <FilterSelect icon={<Route className="h-4 w-4" />} label="Agency" onChange={setAgency} options={["All", ...agencies]} value={agency} />
          <FilterSelect icon={<Target className="h-4 w-4" />} label="Category" onChange={setCategory} options={["All", ...categories]} value={category} />
          <FilterSelect icon={<MapPin className="h-4 w-4" />} label="Zone" onChange={setZone} options={["All", ...zones]} value={zone} />
          <button
            className="inline-flex h-12 items-center justify-center gap-2 rounded-md border border-civic-border px-3 text-sm font-semibold text-civic-primary transition hover:bg-civic-soft"
            onClick={resetFilters}
            type="button"
          >
            <Filter className="h-4 w-4" aria-hidden="true" />
            Clear
          </button>
        </div>
      </Panel>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1.25fr)_minmax(360px,0.75fr)]">
        <Panel
          action={
            <div className="inline-flex items-center gap-2 rounded-md bg-civic-soft px-3 py-2 text-sm font-semibold text-civic-primary">
              <LocateFixed className="h-4 w-4" aria-hidden="true" />
              {filteredIncidents.length} live / {historicalComplaints.length} 311
            </div>
          }
          title="Operations Map"
          description="Live incidents overlaid with historical 311 pressure, duplicate signals, and SLA risk."
        >
          <IncidentOperationsMap
            historicalComplaints={historicalComplaints}
            incidents={filteredIncidents}
            onSelectIncident={(incidentId) => {
              setSelectedId(incidentId);
              setCaseAction("No case action selected");
            }}
            realtimeState={realtimeState}
            selectedIncidentId={selectedIncident?.id ?? ""}
          />
        </Panel>

        <div className="grid gap-6">
          <Panel title="Hot Zones" description="Filtered incident pressure by city zone.">
            <div className="grid gap-3">
              {hotZones.map((item) => (
                <button
                  className={`rounded-md border p-3 text-left transition hover:border-civic-primary hover:bg-civic-soft ${
                    zone === String(item.zone) ? "border-civic-primary bg-civic-soft" : "border-civic-border bg-civic-raised"
                  }`}
                  key={item.zone}
                  onClick={() => setZone(String(item.zone))}
                  type="button"
                >
                  <div className="flex items-center justify-between gap-3 text-sm">
                    <span className="font-semibold text-civic-heading">Zone {item.zone}</span>
                    <span className="text-civic-muted">{item.count} reports</span>
                  </div>
                  <div className="mt-3">
                    <ScoreBar score={Math.min(100, item.count * 22)} />
                  </div>
                </button>
              ))}
            </div>
          </Panel>

          <Panel title="Duplicate Patterns" description="Cases with semantic or nearby-report risk.">
            <div className="grid gap-2">
              {duplicateSignals.length ? (
                duplicateSignals.slice(0, 4).map((incident) => (
                  <button
                    className={`rounded-md border p-3 text-left transition hover:bg-civic-soft ${
                      selectedIncident?.id === incident.id ? "border-civic-primary bg-civic-soft" : "border-civic-border bg-civic-raised"
                    }`}
                    key={incident.id}
                    onClick={() => {
                      setSelectedId(incident.id);
                      setDetailTab("Similar");
                    }}
                    type="button"
                  >
                    <div className="flex items-center justify-between gap-3">
                      <span className="font-semibold text-civic-heading">{incident.id}</span>
                      <span className="text-sm font-semibold text-civic-primary">{incident.duplicates.length} links</span>
                    </div>
                    <p className="mt-1 text-sm text-civic-muted">{incident.location}</p>
                  </button>
                ))
              ) : (
                <div className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm text-civic-muted">
                  No duplicate patterns in this filtered view.
                </div>
              )}
            </div>
          </Panel>

          <Panel title="311 Historical Context" description="Imported historical complaints matching the active search, category, and agency filters.">
            <div className="grid gap-3">
              <div className="rounded-md border border-civic-border bg-civic-raised p-3">
                <div className="flex items-center justify-between gap-3 text-sm">
                  <span className="font-semibold text-civic-heading">Historical records</span>
                  <span className="font-semibold text-civic-primary">{historicalSummary?.totalCount ?? historicalComplaints.length}</span>
                </div>
                <div className="mt-2 text-xs font-semibold text-civic-muted">{historicalStateLabel(historicalState)}</div>
              </div>

              {(historicalSummary?.topCategories ?? []).slice(0, 4).map((bucket) => (
                <button
                  className={`rounded-md border p-3 text-left transition hover:border-civic-primary hover:bg-civic-soft ${
                    category === bucket.value ? "border-civic-primary bg-civic-soft" : "border-civic-border bg-civic-raised"
                  }`}
                  key={bucket.value}
                  onClick={() => setCategory(bucket.value)}
                  type="button"
                >
                  <div className="flex items-center justify-between gap-3 text-sm">
                    <span className="font-semibold text-civic-heading">{bucket.value}</span>
                    <span className="text-civic-muted">{bucket.count} records</span>
                  </div>
                  <div className="mt-3">
                    <ScoreBar score={Math.min(100, bucket.count * 10)} />
                  </div>
                </button>
              ))}

              {historicalState === "empty" || historicalState === "error" ? (
                <div className="rounded-md border border-civic-border bg-civic-raised p-3 text-sm font-semibold text-civic-muted">
                  Import NYC 311 records from Data Sources, then refresh this page to see historical map context.
                </div>
              ) : null}
            </div>
          </Panel>
        </div>
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(340px,0.8fr)_minmax(0,1.2fr)]">
        <Panel
          action={
            <div className="inline-flex items-center gap-2 rounded-md bg-civic-soft px-3 py-2 text-sm font-semibold text-civic-primary">
              <Filter className="h-4 w-4" aria-hidden="true" />
              {filteredIncidents.length} cases
            </div>
          }
          title="Case Queue"
          description="Queue order follows current filters and map selection."
        >
          <div className="grid max-h-[640px] gap-2 overflow-y-auto pr-1">
            {filteredIncidents.map((incident) => (
              <button
                className={`rounded-md border p-4 text-left transition ${
                  selectedIncident?.id === incident.id
                    ? "border-civic-primary bg-civic-soft"
                    : "border-civic-border bg-civic-raised hover:border-civic-border-strong"
                }`}
                key={incident.id}
                onClick={() => {
                  setSelectedId(incident.id);
                  setCaseAction("No case action selected");
                }}
                type="button"
              >
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="break-all font-semibold text-civic-heading">{formatCaseId(incident.id)}</span>
                      <SeverityBadge severity={incident.severity} />
                    </div>
                    <p className="mt-1 font-medium text-civic-ink">{incident.title}</p>
                    <p className="mt-1 text-sm text-civic-muted">{incident.location}</p>
                  </div>
                  <StatusBadge status={incident.status} />
                </div>
              </button>
            ))}
            {!filteredIncidents.length ? (
              <div className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm font-semibold text-civic-muted">
                No live backend incidents match the current filters.
              </div>
            ) : null}
          </div>
        </Panel>

        {selectedIncident ? (
        <Panel
          action={<SegmentedControl onChange={setDetailTab} options={detailTabs} value={detailTab} />}
          title={selectedIncident.title}
          description={`${selectedIncident.location} - ${selectedIncident.age}`}
        >
          <div className="mb-5 rounded-md border border-civic-border bg-civic-raised p-3 text-sm font-semibold text-civic-primary">
            {caseAction}
          </div>

          <div className="mb-5 grid gap-4 md:grid-cols-4">
            <InfoTile icon={<Route className="h-4 w-4" />} label="Agency" value={selectedIncident.agency} />
            <InfoTile icon={<ShieldCheck className="h-4 w-4" />} label="Team" value={selectedIncident.assignedTeam} />
            <InfoTile icon={<Clock3 className="h-4 w-4" />} label="Channel" value={selectedIncident.channel} />
            <InfoTile
              icon={<History className="h-4 w-4" />}
              label="Nearby 311"
              value={nearestHistoricalComplaint ? `${nearbyHistoricalCount} within 500m` : "No nearby records"}
            />
          </div>

          <StaffWorkflowCard
            actionBusy={caseActionBusy}
            incident={selectedIncident}
            onAssign={() => void assignSelectedIncident()}
            onDispatch={() => void dispatchSelectedIncident()}
            onMerge={() => void markSelectedIncidentDuplicate()}
          />

          {detailTab === "Details" ? (
            <div className="grid gap-5">
              <p className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm leading-6 text-civic-muted">
                {selectedIncident.description}
              </p>
              <div className="grid gap-4 md:grid-cols-2">
                <ScoreBar label="AI confidence" score={selectedIncident.confidence * 100} />
                <ScoreBar label="SLA risk" score={selectedIncident.slaRisk} />
              </div>
              <div className="rounded-md border border-civic-border bg-civic-raised p-4">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <div className="flex items-center gap-2 font-semibold text-civic-heading">
                      <Database className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                      Nearby 311 Context
                    </div>
                    <p className="mt-1 text-sm leading-6 text-civic-muted">
                      Historical complaints closest to this incident from the imported public dataset.
                    </p>
                  </div>
                  <span className="shrink-0 rounded-md bg-civic-soft px-3 py-2 text-sm font-semibold text-civic-primary">
                    {nearestHistoricalComplaint ? formatDistance(nearestHistoricalComplaint.distanceMeters) : historicalStateLabel(historicalState)}
                  </span>
                </div>

                {nearbyHistoricalComplaints.length ? (
                  <div className="mt-4 grid gap-3">
                    <div className="grid gap-3 sm:grid-cols-3">
                      <ContextStat
                        label="Closest report"
                        value={nearestHistoricalComplaint ? formatDistance(nearestHistoricalComplaint.distanceMeters) : "None"}
                      />
                      <ContextStat label="Local pressure" value={`${nearbyHistoricalCount} near case`} />
                      <ContextStat label="Top category" value={historicalSummary?.topCategories[0]?.value ?? "Mixed"} />
                    </div>

                    <div className="grid gap-2">
                      {nearbyHistoricalComplaints.slice(0, 4).map((item) => (
                        <div className="rounded-md border border-civic-border bg-civic-surface p-3" key={item.complaint.id}>
                          <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                            <div>
                              <div className="font-semibold text-civic-heading">{item.complaint.complaintType}</div>
                              <p className="mt-1 text-sm leading-6 text-civic-muted">
                                {[item.complaint.agency, item.complaint.borough, item.complaint.status].filter(Boolean).join(" - ")}
                              </p>
                            </div>
                            <span className="shrink-0 rounded-md bg-civic-soft px-2 py-1 text-xs font-semibold text-civic-primary">
                              {formatDistance(item.distanceMeters)}
                            </span>
                          </div>
                          <p className="mt-2 text-sm text-civic-muted">
                            {item.complaint.incidentAddress || item.complaint.descriptor || "No address detail"} - {formatHistoricalDate(item.complaint.createdAt)}
                          </p>
                        </div>
                      ))}
                    </div>
                  </div>
                ) : (
                  <div className="mt-4 rounded-md border border-civic-border bg-civic-surface p-3 text-sm font-semibold text-civic-muted">
                    No imported 311 complaints match this incident area yet.
                  </div>
                )}
              </div>
              <div className="grid gap-3">
                {selectedIncident.evidence.map((item) => (
                  <div className="rounded-md border border-civic-border bg-civic-raised p-4" key={item.title}>
                    <div className="flex items-center justify-between gap-3">
                      <div className="flex items-center gap-2 text-sm font-semibold">
                        <MessageSquareText className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                        {item.title}
                      </div>
                      <span className="text-sm font-semibold text-civic-primary">{Math.round(item.confidence * 100)}%</span>
                    </div>
                    <p className="mt-2 text-sm leading-6 text-civic-muted">{item.detail}</p>
                  </div>
                ))}
              </div>
            </div>
          ) : null}

          {detailTab === "Agent" ? (
            <AgentWorkflowPanel
              incident={selectedIncident}
              message={selectedAgentRun?.message}
              onRun={() => void runSelectedAgentWorkflow()}
              runState={selectedAgentRun?.state ?? "idle"}
              workflow={selectedAgentRun?.workflow}
            />
          ) : null}

          {detailTab === "Similar" ? (
            <div className="grid gap-3">
              {selectedIncident.duplicates.length ? (
                selectedIncident.duplicates.map((duplicate) => (
                  <div className="rounded-md border border-civic-border bg-civic-raised p-4" key={duplicate.caseId}>
                    <div className="flex items-center justify-between">
                      <span className="break-all font-semibold text-civic-heading">{formatCaseId(duplicate.caseId)}</span>
                      <span className="text-sm font-semibold text-civic-primary">{Math.round(duplicate.score * 100)}% match</span>
                    </div>
                    <p className="mt-2 text-sm text-civic-muted">{duplicate.distanceMeters} meters from selected incident</p>
                    <div className="mt-3">
                      <ScoreBar score={duplicate.score * 100} />
                    </div>
                  </div>
                ))
              ) : (
                <div className="rounded-md border border-civic-border bg-civic-raised p-5 text-sm text-civic-muted">
                  No duplicate candidates above the review threshold.
                </div>
              )}
            </div>
          ) : null}

          {detailTab === "Timeline" ? (
            <div className="grid gap-3">
              {selectedIncident.timeline.map((event) => (
                <div className="rounded-md border border-civic-border bg-civic-raised p-4" key={`${event.label}-${event.time}`}>
                  <div className="flex items-center justify-between gap-4">
                    <span className="font-semibold text-civic-heading">{event.label}</span>
                    <span className="text-sm text-civic-muted">{event.time}</span>
                  </div>
                  <p className="mt-2 text-sm leading-6 text-civic-muted">{event.detail}</p>
                </div>
              ))}
            </div>
          ) : null}

        </Panel>
        ) : (
          <Panel title="No Incident Selected" description="The operations workspace is connected to live backend incidents only.">
            <div className="rounded-md border border-civic-border bg-civic-raised p-5 text-sm leading-6 text-civic-muted">
              Submit a new report from the citizen portal, import seed data through the backend, or clear filters after incidents are available.
            </div>
          </Panel>
        )}
      </div>
    </div>
  );
}

function FilterSelect({
  icon,
  label,
  onChange,
  options,
  value,
}: {
  icon: ReactNode;
  label: string;
  onChange: (value: string) => void;
  options: readonly string[];
  value: string;
}) {
  return (
    <label className="flex h-12 items-center gap-2 rounded-md border border-civic-border bg-civic-raised px-3 text-sm text-civic-muted">
      <span className="text-civic-primary">{icon}</span>
      <span className="sr-only">{label}</span>
      <select
        aria-label={label}
        className="min-w-0 flex-1 bg-transparent text-sm font-semibold outline-none"
        onChange={(event) => onChange(event.target.value)}
        value={value}
      >
        {options.map((option) => (
          <option key={option} value={option}>
            {option === "All" ? `${label}: All` : option}
          </option>
        ))}
      </select>
    </label>
  );
}

function InfoTile({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-raised p-4">
      <div className="flex items-center gap-2 text-sm text-civic-muted">
        <span className="text-civic-primary">{icon}</span>
        {label}
      </div>
      <div className="mt-2 font-semibold text-civic-heading">{value}</div>
    </div>
  );
}

function StaffWorkflowCard({
  actionBusy,
  incident,
  onAssign,
  onDispatch,
  onMerge,
}: {
  actionBusy: "assign" | "dispatch" | "merge" | null;
  incident: IncidentRow;
  onAssign: () => void;
  onDispatch: () => void;
  onMerge: () => void;
}) {
  const workflow = buildStaffWorkflow(incident);
  const approved = isWorkflowComplete(incident, "Approved");
  const assigned = isWorkflowComplete(incident, "Assigned");
  const dispatched = isWorkflowComplete(incident, "Dispatched");
  const canAssign = approved && !assigned && !dispatched && actionBusy === null;
  const canDispatch = assigned && !dispatched && actionBusy === null;
  const canMerge = !dispatched && actionBusy === null;

  return (
    <div className="mb-5 rounded-lg border border-civic-border bg-civic-raised p-4">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <div className="inline-flex items-center gap-2 rounded-md bg-civic-soft px-2 py-1 text-xs font-semibold uppercase tracking-[0.12em] text-civic-primary">
            <ClipboardCheck className="h-4 w-4" aria-hidden="true" />
            Staff workflow
          </div>
          <h3 className="mt-3 text-lg font-semibold text-civic-heading">Review, approve, assign, dispatch</h3>
          <p className="mt-1 text-sm leading-6 text-civic-muted">
            Actions update the backend incident record and appear in the timeline tab.
          </p>
        </div>
        <StatusBadge status={incident.status} />
      </div>

      <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-5">
        {workflow.map((step) => (
          <div
            className={`rounded-md border p-3 ${
              step.complete
                ? "border-civic-primary/30 bg-civic-soft"
                : step.current
                  ? "border-status-review bg-status-review/20"
                  : "border-civic-border bg-civic-surface"
            }`}
            key={step.label}
          >
            <div className="flex items-center gap-2">
              <CheckCircle2 className={`h-4 w-4 ${step.complete ? "text-civic-primary" : "text-civic-muted"}`} aria-hidden="true" />
              <span className="text-sm font-semibold text-civic-heading">{step.label}</span>
            </div>
            <p className="mt-2 text-xs leading-5 text-civic-muted">{step.detail}</p>
          </div>
        ))}
      </div>

      <div className="mt-4 grid gap-3 border-t border-civic-border pt-4 sm:grid-cols-4">
        <Link
          className="inline-flex h-11 items-center justify-center gap-2 rounded-md bg-civic-primary px-3 text-sm font-semibold text-white transition hover:bg-civic-primary-strong"
          href="/admin/review"
        >
          <ClipboardCheck className="h-4 w-4" aria-hidden="true" />
          Review / Approve
        </Link>
        <button
          className="inline-flex h-11 items-center justify-center gap-2 rounded-md border border-civic-border px-3 text-sm font-semibold text-civic-primary transition hover:bg-civic-soft disabled:cursor-not-allowed disabled:opacity-60"
          disabled={!canAssign}
          onClick={onAssign}
          type="button"
        >
          <UserCheck className="h-4 w-4" aria-hidden="true" />
          {actionBusy === "assign" ? "Assigning" : assigned ? "Assigned" : "Assign"}
        </button>
        <button
          className="inline-flex h-11 items-center justify-center gap-2 rounded-md border border-civic-border px-3 text-sm font-semibold text-civic-primary transition hover:bg-civic-soft disabled:cursor-not-allowed disabled:opacity-60"
          disabled={!canDispatch}
          onClick={onDispatch}
          type="button"
        >
          <Send className="h-4 w-4" aria-hidden="true" />
          {actionBusy === "dispatch" ? "Dispatching" : dispatched ? "Dispatched" : "Dispatch"}
        </button>
        <button
          className="inline-flex h-11 items-center justify-center gap-2 rounded-md border border-civic-border px-3 text-sm font-semibold text-civic-primary transition hover:bg-civic-soft disabled:cursor-not-allowed disabled:opacity-60"
          disabled={!canMerge}
          onClick={onMerge}
          type="button"
        >
          <GitMerge className="h-4 w-4" aria-hidden="true" />
          {actionBusy === "merge" ? "Linking" : "Link Duplicate"}
        </button>
      </div>
    </div>
  );
}

function ContextStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-surface p-3">
      <div className="text-xs font-semibold uppercase text-civic-muted">{label}</div>
      <div className="mt-1 text-sm font-semibold text-civic-heading">{value}</div>
    </div>
  );
}

function buildStaffWorkflow(incident: IncidentRow) {
  const reviewed = isWorkflowComplete(incident, "Reviewed");
  const approved = isWorkflowComplete(incident, "Approved");
  const assigned = isWorkflowComplete(incident, "Assigned");
  const dispatched = isWorkflowComplete(incident, "Dispatched");

  return [
    {
      complete: true,
      current: false,
      detail: "Citizen report is stored with coordinates and evidence metadata.",
      label: "Submitted",
    },
    {
      complete: reviewed,
      current: !reviewed,
      detail: reviewed ? "Reviewer decision is recorded." : "Open the review bench to confirm AI evidence.",
      label: "Review",
    },
    {
      complete: approved,
      current: reviewed && !approved,
      detail: approved ? "Case is approved for operations." : "Approve the case after review corrections are complete.",
      label: "Approve",
    },
    {
      complete: assigned,
      current: approved && !assigned,
      detail: assigned ? `Assigned to ${incident.assignedTeam}.` : "Assign the approved case to the agency queue.",
      label: "Assign",
    },
    {
      complete: dispatched,
      current: assigned && !dispatched,
      detail: dispatched ? "Field response has been dispatched." : "Dispatch after an assignment exists.",
      label: "Dispatch",
    },
  ];
}

function isWorkflowComplete(incident: IncidentRow, step: "Reviewed" | "Approved" | "Assigned" | "Dispatched") {
  if (step === "Reviewed") {
    return hasTimelineEvent(incident, "Reviewed") || incident.status === "Approved" || incident.status === "Dispatched";
  }

  if (step === "Approved") {
    return hasTimelineEvent(incident, "Approved") || incident.status === "Approved" || incident.status === "Dispatched";
  }

  if (step === "Assigned") {
    return hasTimelineEvent(incident, "Assigned");
  }

  return incident.status === "Dispatched" || hasTimelineEvent(incident, "Dispatched");
}

function hasTimelineEvent(incident: IncidentRow, label: string) {
  return incident.timeline.some((event) => event.label === label);
}

function mapBackendIncident(
  incident: IncidentDto,
  duplicateCandidates: DuplicateCandidateDto[] = [],
  prediction: TriagePredictionDto | null = null,
): IncidentRow {
  const category = incident.correctedCategory ?? prediction?.category ?? inferCategory(incident.description);
  const severity = normalizeSeverity(incident.correctedSeverity ?? prediction?.severity ?? inferSeverity(incident.description));
  const agency = incident.assignedAgencyCode ?? incident.correctedAgencyCode ?? prediction?.suggestedAgencyCode ?? inferAgency(category);
  const status = normalizeStatus(incident.status);
  const confidence = clamp(prediction?.confidence ?? (incident.acceptedPrediction ? 0.86 : 0.58), 0, 1);
  const duplicates = buildDuplicateRows(incident, duplicateCandidates);
  const evidence = prediction?.evidence.length
    ? prediction.evidence.map((item) => ({
        confidence: clamp(item.confidence ?? confidence, 0, 1),
        detail: item.detail,
        title: item.title,
      }))
    : [
        {
          confidence,
          detail: "Backend incident record contains description, coordinates, status, and review metadata.",
          title: "Backend record",
        },
      ];

  return {
    age: formatAge(incident.createdAt),
    agency,
    aiSummary: prediction?.summary ?? `${statusLabel(category)} report loaded from backend persistence with geospatial coordinates.`,
    assignedTeam: incident.assignedTeam ?? `${agency} intake queue`,
    category,
    channel: "Web",
    cityZone: estimateZone(incident.latitude, incident.longitude),
    confidence,
    coordinates: {
      latitude: incident.latitude,
      longitude: incident.longitude,
    },
    description: incident.description,
    duplicates,
    evidence,
    id: incident.id,
    location: `${incident.latitude.toFixed(5)}, ${incident.longitude.toFixed(5)}`,
    reporter: "Citizen portal",
    severity,
    slaRisk: calculateSlaRisk(status, severity, confidence),
    status,
    timeline: [
      {
        detail: "Incident was created through the API.",
        label: "Submitted",
        time: formatAge(incident.createdAt),
      },
      ...(prediction
        ? [
            {
              detail: `${prediction.modelName}${prediction.modelVersion ? ` ${prediction.modelVersion}` : ""} predicted ${severity} ${category} for ${agency} at ${Math.round(confidence * 100)}% confidence.`,
              label: "AI triage",
              time: formatAge(prediction.createdAt),
            },
          ]
        : []),
      ...(incident.reviewDecision
        ? [
            {
              detail: incident.reviewNote ?? `Review decision: ${incident.reviewDecision}`,
              label: "Reviewed",
              time: incident.reviewedAt ? formatAge(incident.reviewedAt) : "Recorded",
            },
          ]
        : []),
      ...(incident.reviewDecision === "Approved"
        ? [
            {
              detail: "Approved for agency assignment and response.",
              label: "Approved",
              time: incident.reviewedAt ? formatAge(incident.reviewedAt) : "Recorded",
            },
          ]
        : []),
      ...(incident.assignedAt
        ? [
            {
              detail: `Assigned to ${incident.assignedTeam ?? `${agency} intake queue`}.`,
              label: "Assigned",
              time: formatAge(incident.assignedAt),
            },
          ]
        : []),
      ...(incident.duplicateLinkedAt && incident.duplicateOfIncidentId
        ? [
            {
              detail: `Linked as duplicate of ${formatCaseId(incident.duplicateOfIncidentId)}.`,
              label: "Duplicate linked",
              time: formatAge(incident.duplicateLinkedAt),
            },
          ]
        : []),
      ...(incident.dispatchedAt
        ? [
            {
              detail: "Dispatched to field operations.",
              label: "Dispatched",
              time: formatAge(incident.dispatchedAt),
            },
          ]
        : []),
    ],
    title: `${statusLabel(category)} report`,
  };
}

function buildDuplicateRows(incident: IncidentDto, duplicateCandidates: DuplicateCandidateDto[]) {
  const rows = new Map<string, { caseId: string; distanceMeters: number; score: number }>();

  if (incident.duplicateOfIncidentId) {
    rows.set(incident.duplicateOfIncidentId, {
      caseId: incident.duplicateOfIncidentId,
      distanceMeters: 0,
      score: 0.95,
    });
  }

  duplicateCandidates.forEach((candidate) => {
    rows.set(candidate.candidateIncidentId, {
      caseId: candidate.candidateIncidentId,
      distanceMeters: 0,
      score: candidate.similarityScore,
    });
  });

  return Array.from(rows.values()).sort((left, right) => right.score - left.score);
}

function applyRealtimeEventToRows(rows: IncidentRow[], event: IncidentRealtimeEventDto) {
  const incoming = event.incident ? mapBackendIncident(event.incident) : null;
  let found = false;

  const updatedRows = rows.map((row) => {
    if (row.id !== event.incidentId) {
      return row;
    }

    found = true;
    const status = normalizeStatus(event.incidentStatus);
    const baseRow = incoming ?? {
      ...row,
      slaRisk: calculateSlaRisk(status, row.severity, row.confidence),
      status,
      timeline: event.processingStatus.steps.length ? processingTimeline(event.processingStatus.steps) : row.timeline,
    };

    return mergePredictionEvent(baseRow, event);
  });

  if (!found && incoming) {
    return [mergePredictionEvent(incoming, event), ...rows];
  }

  return updatedRows;
}

function mergePredictionEvent(row: IncidentRow, event: IncidentRealtimeEventDto): IncidentRow {
  if (!event.prediction) {
    return row;
  }

  const category = event.prediction.category;
  const severity = normalizeSeverity(event.prediction.severity);
  const status = normalizeStatus(event.incidentStatus);
  const confidence = event.prediction.confidence;

  return {
    ...row,
    agency: event.prediction.suggestedAgencyCode,
    aiSummary: event.prediction.summary,
    assignedTeam: `${event.prediction.suggestedAgencyCode} intake queue`,
    category,
    confidence,
    duplicates: event.duplicateCandidates.map((candidate) => ({
      caseId: candidate.candidateIncidentId,
      distanceMeters: 0,
      score: candidate.similarityScore,
    })),
    evidence: event.prediction.evidence.map((item) => ({
      confidence: item.confidence ?? 0,
      detail: item.detail,
      title: item.title,
    })),
    severity,
    slaRisk: calculateSlaRisk(status, severity, confidence),
    status,
    title: `${statusLabel(category)} report`,
  };
}

function processingTimeline(steps: ProcessingStepDto[]) {
  return steps.map((step) => {
    const stepName = statusLabel(step.name.replace(/[_-]/g, " "));
    const timestamp = step.completedAt ?? step.startedAt ?? step.updatedAt;

    return {
      detail: step.errorMessage ?? `${stepName} is ${statusLabel(step.status)}.`,
      label: stepName,
      time: formatAge(timestamp),
    };
  });
}

function getHotZones(items: IncidentRow[]) {
  const counts = new Map<number, number>();

  items.forEach((incident) => {
    counts.set(incident.cityZone, (counts.get(incident.cityZone) ?? 0) + 1);
  });

  return Array.from(counts.entries())
    .map(([zone, count]) => ({ count, zone }))
    .sort((left, right) => right.count - left.count || left.zone - right.zone)
    .slice(0, 4);
}

function getNearbyHistoricalComplaints(incident: IncidentRow, complaints: HistoricalComplaintDto[], limit: number) {
  return complaints
    .map((complaint) => ({
      complaint,
      distanceMeters: calculateDistanceMeters(
        incident.coordinates.latitude,
        incident.coordinates.longitude,
        complaint.latitude,
        complaint.longitude,
      ),
    }))
    .sort((left, right) => left.distanceMeters - right.distanceMeters)
    .slice(0, limit);
}

function loadSourceLabel(state: LoadState) {
  if (state === "loading") {
    return "Loading live API";
  }

  if (state === "live") {
    return "Live backend data";
  }

  if (state === "error") {
    return "Live API unavailable";
  }

  return "No backend incidents";
}

function historicalStateLabel(state: HistoricalLoadState) {
  if (state === "loading") {
    return "Loading NYC 311 history";
  }

  if (state === "live") {
    return "Imported NYC 311 records";
  }

  if (state === "empty") {
    return "No 311 records imported yet";
  }

  if (state === "error") {
    return "311 API unavailable";
  }

  return "Historical context standby";
}

function realtimeLabel(state: RealtimeConnectionState) {
  if (state === "connected") {
    return "Realtime live";
  }

  if (state === "connecting") {
    return "Connecting live";
  }

  if (state === "reconnecting") {
    return "Reconnecting live";
  }

  if (state === "offline") {
    return "Realtime offline";
  }

  return "Realtime standby";
}

function realtimeClassName(state: RealtimeConnectionState) {
  if (state === "connected") {
    return "border-status-approved bg-status-approved/10 text-status-approved-text";
  }

  if (state === "connecting" || state === "reconnecting") {
    return "border-status-review bg-status-review/10 text-status-review-text";
  }

  if (state === "offline") {
    return "border-status-critical bg-status-critical/10 text-status-critical-text";
  }

  return "border-civic-border bg-civic-raised text-civic-muted";
}

function normalizeStatus(status: string): IncidentStatus {
  const knownStatuses: IncidentStatus[] = ["Submitted", "Triaged", "HumanReviewRequired", "Approved", "Dispatched"];
  return knownStatuses.includes(status as IncidentStatus) ? (status as IncidentStatus) : "Submitted";
}

function normalizeSeverity(severity?: string | null): Severity {
  const knownSeverities: Severity[] = ["Low", "Medium", "High", "Critical"];
  return knownSeverities.includes(severity as Severity) ? (severity as Severity) : "Medium";
}

function inferCategory(description: string) {
  const lower = description.toLowerCase();

  if (lower.includes("drain") || lower.includes("water") || lower.includes("flood")) {
    return "Drainage";
  }

  if (lower.includes("sidewalk")) {
    return "Sidewalk";
  }

  if (lower.includes("signal") || lower.includes("streetlight") || lower.includes("light")) {
    return "TrafficSignal";
  }

  return "RoadDamage";
}

function inferSeverity(description: string): Severity {
  const lower = description.toLowerCase();

  if (lower.includes("dark") || lower.includes("outage") || lower.includes("swerving") || lower.includes("blocked")) {
    return "High";
  }

  if (lower.includes("hazard") || lower.includes("flood")) {
    return "Critical";
  }

  return "Medium";
}

function inferAgency(category: string) {
  if (category === "RoadDamage" || category === "TrafficSignal") {
    return "DOT";
  }

  if (category === "Drainage" || category === "Sidewalk") {
    return "DPW";
  }

  return "Operations";
}

function estimateZone(latitude: number, longitude: number) {
  return (Math.abs(Math.round((latitude + longitude) * 100)) % 30) + 1;
}

function calculateSlaRisk(status: IncidentStatus, severity: Severity, confidence: number) {
  const severityBase: Record<Severity, number> = {
    Critical: 88,
    High: 72,
    Low: 24,
    Medium: 48,
  };
  const statusAdjustment = status === "HumanReviewRequired" ? 12 : status === "Dispatched" ? 8 : status === "Approved" ? -18 : 0;

  return clamp(Math.round(severityBase[severity] + statusAdjustment + (1 - confidence) * 18), 10, 100);
}

function formatAge(value: string) {
  const created = new Date(value).getTime();
  if (Number.isNaN(created)) {
    return "Just now";
  }

  const minutes = Math.max(0, Math.round((Date.now() - created) / 60000));

  if (minutes < 60) {
    return `${minutes} min`;
  }

  return `${Math.round(minutes / 60)} hr`;
}

function formatCaseId(value: string) {
  return value.length > 18 ? `${value.slice(0, 8)}...${value.slice(-6)}` : value;
}

function formatRealtimeTime(value: string) {
  return new Date(value).toLocaleTimeString([], {
    hour: "numeric",
    minute: "2-digit",
  });
}

function formatHistoricalDate(value: string) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "Date unavailable";
  }

  return date.toLocaleDateString([], {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

function formatDistance(distanceMeters: number) {
  if (distanceMeters < 1000) {
    return `${Math.round(distanceMeters)}m`;
  }

  return `${(distanceMeters / 1000).toFixed(1)}km`;
}

function uniqueSorted(values: string[]) {
  return Array.from(new Set(values)).sort((left, right) => left.localeCompare(right));
}

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}

function calculateDistanceMeters(latitudeA: number, longitudeA: number, latitudeB: number, longitudeB: number) {
  const earthRadiusMeters = 6_371_000;
  const latitudeDelta = degreesToRadians(latitudeB - latitudeA);
  const longitudeDelta = degreesToRadians(longitudeB - longitudeA);
  const startLatitude = degreesToRadians(latitudeA);
  const endLatitude = degreesToRadians(latitudeB);
  const halfChord =
    Math.sin(latitudeDelta / 2) ** 2 +
    Math.cos(startLatitude) * Math.cos(endLatitude) * Math.sin(longitudeDelta / 2) ** 2;

  return 2 * earthRadiusMeters * Math.atan2(Math.sqrt(halfChord), Math.sqrt(1 - halfChord));
}

function degreesToRadians(value: number) {
  return (value * Math.PI) / 180;
}
