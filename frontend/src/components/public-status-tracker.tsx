"use client";

import type { FormEvent } from "react";
import { useEffect, useMemo, useRef, useState, useSyncExternalStore } from "react";
import type { LucideIcon } from "lucide-react";
import {
  AlertCircle,
  Bell,
  CheckCircle2,
  ClipboardCheck,
  Copy,
  History,
  RadioTower,
  Search,
  Send,
  ShieldCheck,
  Sparkles,
  Star,
  Trash2,
  Truck,
  UserCheck,
} from "lucide-react";
import { statusLabel } from "@/lib/civic-analysis";
import {
  CivicApiError,
  getOptional,
  getPublicDuplicateCandidates,
  getPublicIncident,
  getPublicIncidentStatus,
  getPublicLatestPrediction,
  isTrackingCode,
  requestPublicIncidentUpdate,
  submitPublicIncidentFeedback,
  updatePublicNotificationPreference,
  type DuplicateCandidateDto,
  type IncidentDto,
  type IncidentProcessingStatusDto,
  type ProcessingStepDto,
  type TriagePredictionDto,
} from "@/lib/civic-api";
import {
  clearPublicReportHistory,
  createStoredPublicReport,
  getPublicReportHistoryServerSnapshot,
  readPublicReportHistory,
  savePublicReport,
  subscribePublicReportHistory,
  type StoredPublicReport,
} from "@/lib/public-report-history";
import {
  createIncidentHubConnection,
  incidentRealtimeEvents,
  incidentRealtimeEventTypes,
  isIncidentRealtimeAvailable,
  type IncidentRealtimeEventDto,
  type RealtimeConnectionState,
} from "@/lib/civic-realtime";
import {
  getBrowserNotificationPermission,
  requestBrowserNotificationPermission,
  showReportNotification,
  type BrowserNotificationPermission,
} from "@/lib/browser-notifications";
import type { IncidentRow, IncidentStatus, Severity } from "@/lib/civic-types";
import { PublicReportLocationMap } from "@/components/public-report-location-map";
import { fieldClassName, PageHeader, Panel, ScoreBar, SeverityBadge, StatusBadge } from "@/components/ui-kit";

type TimelineItem = {
  complete: boolean;
  detail: string;
  icon: LucideIcon;
  label: string;
  shortDetail: string;
  time?: string;
};

export function PublicStatusTracker({ initialCode }: { initialCode?: string }) {
  const defaultCode = initialCode?.trim() ?? "";
  const [code, setCode] = useState(defaultCode);
  const [backendIncident, setBackendIncident] = useState<IncidentDto | null>(null);
  const [backendStatus, setBackendStatus] = useState<IncidentProcessingStatusDto | null>(null);
  const [backendPrediction, setBackendPrediction] = useState<TriagePredictionDto | null>(null);
  const [backendDuplicates, setBackendDuplicates] = useState<DuplicateCandidateDto[]>([]);
  const [lookupState, setLookupState] = useState<"idle" | "loading" | "live" | "error">("idle");
  const [lookupMessage, setLookupMessage] = useState("Enter the tracking code from a submitted CivicSignal report.");
  const [realtimeState, setRealtimeState] = useState<RealtimeConnectionState>("idle");
  const [realtimeMessage, setRealtimeMessage] = useState("Realtime connects after a live report loads.");
  const [alerts, setAlerts] = useState(true);
  const [alertState, setAlertState] = useState("Status alerts are ready.");
  const [alertPending, setAlertPending] = useState(false);
  const [notificationPermission, setNotificationPermission] = useState<BrowserNotificationPermission>(() =>
    getBrowserNotificationPermission(),
  );
  const [selectedMilestone, setSelectedMilestone] = useState("Review");
  const [message, setMessage] = useState("Could you notify me when a crew is assigned?");
  const [requestState, setRequestState] = useState("No update requested");
  const [updateRequestPending, setUpdateRequestPending] = useState(false);
  const [rating, setRating] = useState(4);
  const [feedbackState, setFeedbackState] = useState("No feedback sent");
  const [feedbackPending, setFeedbackPending] = useState(false);
  const [copyState, setCopyState] = useState("Copy");
  const recentReports = useSyncExternalStore(
    subscribePublicReportHistory,
    readPublicReportHistory,
    getPublicReportHistoryServerSnapshot,
  );
  const alertsRef = useRef(alerts);

  const incident = useMemo(
    () => (backendIncident ? mapBackendIncident(backendIncident, backendPrediction, backendDuplicates) : null),
    [backendDuplicates, backendIncident, backendPrediction],
  );
  const timeline = useMemo(
    () => buildPublicReportTimeline(backendIncident, backendPrediction, backendStatus),
    [backendIncident, backendPrediction, backendStatus],
  );
  const visibleSelectedMilestone = timeline.some((item) => item.label === selectedMilestone)
    ? selectedMilestone
    : timeline[0]?.label;
  const activeMilestone = timeline.find((item) => item.label === visibleSelectedMilestone) ?? timeline[0];
  const progress = timeline.length
    ? timelineProgress(timeline)
    : backendStatus?.steps.length
      ? processingProgress(backendStatus.steps)
      : incident
        ? statusProgress(incident.status)
        : 0;
  const liveTrackingCode = lookupState === "live" ? backendIncident?.trackingCode ?? null : null;

  useEffect(() => {
    alertsRef.current = alerts;
  }, [alerts]);

  useEffect(() => {
    if (initialCode && isTrackingCode(initialCode)) {
      void loadBackendReport(initialCode);
    }
  }, [initialCode]);

  useEffect(() => {
    const incidentId = backendIncident?.id;
    const trackingCode = backendIncident?.trackingCode;

    if (!incidentId || !trackingCode) {
      return;
    }

    let active = true;
    let connection: ReturnType<typeof createIncidentHubConnection> | null = null;

    const applyRealtimeEvent = (event: IncidentRealtimeEventDto) => {
      if (!active || event.incidentId !== incidentId) {
        return;
      }

      if (event.incident) {
        setBackendIncident(event.incident);
        setAlerts(event.incident.notificationAlertsEnabled ?? false);
        setAlertState(formatAlertPreferenceState(event.incident));
        savePublicReport(
          createStoredPublicReport(event.incident, {
            media: event.media,
            status: event.processingStatus?.incidentStatus ?? event.incidentStatus,
          }),
        );
      }

      setBackendStatus(event.processingStatus);

      if (event.prediction) {
        setBackendPrediction(event.prediction);
      }

      if (event.eventType === incidentRealtimeEventTypes.analyzed) {
        setBackendDuplicates(event.duplicateCandidates ?? []);
      }

      if (alertsRef.current && event.eventType !== incidentRealtimeEventTypes.notificationPreferenceUpdated) {
        void showReportNotification({
          body: event.message,
          title: "CivicSignal report update",
          trackingCode,
        });
      }

      setLookupState("live");
      setLookupMessage(`Live update: ${event.message}`);
      setRequestState(`Live update ${formatRealtimeTime(event.occurredAt)}: ${event.message}`);
    };

    async function startRealtime() {
      const available = await isIncidentRealtimeAvailable();

      if (!active) {
        return;
      }

      if (!available) {
        setRealtimeState("offline");
        setRealtimeMessage("Realtime hub is offline. Search still refreshes the report.");
        return;
      }

      connection = createIncidentHubConnection();
      connection.on(incidentRealtimeEvents.incidentUpdated, applyRealtimeEvent);
      connection.onreconnecting(() => {
        if (active) {
          setRealtimeState("reconnecting");
          setRealtimeMessage("Realtime connection is reconnecting.");
        }
      });
      connection.onreconnected(async () => {
        if (!active) {
          return;
        }

        await connection?.invoke("SubscribeToTrackingCode", trackingCode);
        setRealtimeState("connected");
        setRealtimeMessage("Listening for live processing updates.");
      });
      connection.onclose(() => {
        if (active) {
          setRealtimeState("offline");
          setRealtimeMessage("Realtime connection is closed. Search still refreshes the report.");
        }
      });

      try {
        await connection.start();

        if (!active) {
          return;
        }

        await connection.invoke("SubscribeToTrackingCode", trackingCode);
        setRealtimeState("connected");
        setRealtimeMessage("Listening for live processing updates.");
      } catch {
        if (active) {
          setRealtimeState("offline");
          setRealtimeMessage("Realtime is unavailable. Search still refreshes the report.");
        }
      }
    }

    void startRealtime();

    return () => {
      active = false;
      connection?.off(incidentRealtimeEvents.incidentUpdated, applyRealtimeEvent);
      void connection?.stop();
    };
  }, [backendIncident?.id, backendIncident?.trackingCode]);

  async function loadBackendReport(trackingCode: string) {
    setLookupState("loading");
    setLookupMessage("Loading live report from the CivicSignal API...");
    setRealtimeState("connecting");
    setRealtimeMessage("Realtime will connect after the report loads.");
    setCopyState("Copy");
    setBackendIncident(null);
    setBackendStatus(null);
    setBackendPrediction(null);
    setBackendDuplicates([]);

    try {
      const [incidentResult, statusResult, predictionResult, duplicateResult] = await Promise.all([
        getPublicIncident(trackingCode),
        getOptional(() => getPublicIncidentStatus(trackingCode)),
        getOptional(() => getPublicLatestPrediction(trackingCode)),
        getOptional(() => getPublicDuplicateCandidates(trackingCode)),
      ]);

      setBackendIncident(incidentResult);
      setAlerts(incidentResult.notificationAlertsEnabled ?? false);
      setAlertState(formatAlertPreferenceState(incidentResult));
      setBackendStatus(statusResult);
      setBackendPrediction(predictionResult);
      setBackendDuplicates(duplicateResult ?? []);
      setLookupState("live");
      setLookupMessage("Live report loaded from the backend API.");
      setRequestState("Report loaded");
      savePublicReport(
        createStoredPublicReport(incidentResult, {
          status: statusResult?.incidentStatus,
        }),
      );
    } catch (error) {
      setLookupState("error");
      setLookupMessage(error instanceof CivicApiError ? error.message : "Could not reach the CivicSignal API.");
      setRealtimeState("offline");
      setRealtimeMessage("Realtime is available after a live report loads.");
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (isTrackingCode(code)) {
      void loadBackendReport(code.trim());
      return;
    }

    setBackendIncident(null);
    setBackendStatus(null);
    setBackendPrediction(null);
    setBackendDuplicates([]);
    setLookupState("error");
    setLookupMessage("Enter a CivicSignal tracking code from a submitted report.");
    setRealtimeState("idle");
    setRealtimeMessage("Realtime connects after a live report loads.");
    setRequestState("No live report loaded");
    setCopyState("Copy");
  }

  async function copyCode() {
    if (!incident) {
      setCopyState("No report");
      return;
    }

    try {
      await navigator.clipboard.writeText(liveTrackingCode ?? incident.id);
      setCopyState("Copied");
    } catch {
      setCopyState("Manual copy");
    }
  }

  async function toggleAlerts() {
    if (!liveTrackingCode) {
      setAlertState("Load a live report before changing alert preferences.");
      return;
    }

    const nextAlerts = !alerts;

    if (nextAlerts) {
      setAlertPending(true);
      setAlertState("Requesting browser notification permission...");

      const permission = await requestBrowserNotificationPermission();
      setNotificationPermission(permission);

      if (permission !== "granted") {
        setAlertPending(false);
        setAlerts(false);
        setAlertState(
          permission === "denied"
            ? "Browser notifications are blocked. Enable them in browser settings to receive alerts."
            : "This browser does not support report notifications.",
        );
        return;
      }
    }

    setAlerts(nextAlerts);

    setAlertPending(true);
    setAlertState(nextAlerts ? "Enabling status alerts..." : "Disabling status alerts...");

    try {
      const preference = await updatePublicNotificationPreference(liveTrackingCode, {
        alertsEnabled: nextAlerts,
        channel: nextAlerts ? "Browser" : "None",
      });

      setAlerts(preference.alertsEnabled);
      setAlertState(`Alerts ${preference.alertsEnabled ? "enabled" : "disabled"} at ${formatRealtimeTime(preference.updatedAt)}.`);
      if (preference.alertsEnabled) {
        void showReportNotification({
          body: "You will receive browser alerts while this report is open or installed as a PWA.",
          title: "CivicSignal alerts enabled",
          trackingCode: liveTrackingCode,
        });
      }
    } catch (error) {
      setAlerts(!nextAlerts);
      setAlertState(getApiErrorMessage(error, "Could not update alert preference."));
    } finally {
      setAlertPending(false);
    }
  }

  async function requestUpdate() {
    const trimmedMessage = message.trim();

    if (!trimmedMessage) {
      setRequestState("Add a short message before sending.");
      return;
    }

    if (!liveTrackingCode) {
      setRequestState("Load a live report before requesting an update.");
      return;
    }

    setUpdateRequestPending(true);
    setRequestState("Sending update request...");

    try {
      const updateRequest = await requestPublicIncidentUpdate(liveTrackingCode, {
        message: trimmedMessage,
      });

      setRequestState(`Update request sent ${formatRealtimeTime(updateRequest.createdAt)}.`);
    } catch (error) {
      setRequestState(getApiErrorMessage(error, "Could not send update request."));
    } finally {
      setUpdateRequestPending(false);
    }
  }

  async function sendFeedback() {
    if (!liveTrackingCode) {
      setFeedbackState("Load a live report before sending feedback.");
      return;
    }

    setFeedbackPending(true);
    setFeedbackState("Sending feedback...");

    try {
      const feedback = await submitPublicIncidentFeedback(liveTrackingCode, {
        comment: "Resident rated status clarity from the public tracking page.",
        rating,
      });

      setFeedbackState(`Feedback sent ${formatRealtimeTime(feedback.createdAt)}: ${feedback.rating}/5.`);
    } catch (error) {
      setFeedbackState(getApiErrorMessage(error, "Could not send feedback."));
    } finally {
      setFeedbackPending(false);
    }
  }

  function loadRecentReport(report: StoredPublicReport) {
    const reportCode = report.trackingCode ?? report.incidentId;
    setCode(reportCode);

    if (isTrackingCode(reportCode)) {
      void loadBackendReport(reportCode);
      return;
    }

    setBackendIncident(null);
    setBackendStatus(null);
    setBackendPrediction(null);
    setBackendDuplicates([]);
    setLookupState("error");
    setLookupMessage("This saved report uses an old local identifier. Open a report with a public tracking code for live status.");
    setRealtimeState("idle");
    setRealtimeMessage("Realtime connects after a live report loads.");
  }

  function clearRecentReports() {
    clearPublicReportHistory();
  }

  return (
    <div className="space-y-6">
      <PageHeader
        description="Residents can check review, routing, duplicate, and agency response status using their report number."
        eyebrow="Resident Portal"
        title="Track Report Status"
      />

      <div className="grid gap-6 xl:grid-cols-[minmax(0,0.85fr)_minmax(360px,0.75fr)]">
        <Panel title="Find Your Report" description="Use the tracking code returned after submitting a report.">
          <form className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto]" onSubmit={handleSubmit}>
            <label className="relative block">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-civic-muted" aria-hidden="true" />
              <input className={`${fieldClassName} pl-10`} onChange={(event) => setCode(event.target.value)} value={code} />
            </label>
            <button
              className="inline-flex h-12 items-center justify-center gap-2 rounded-md bg-civic-primary px-5 text-base font-semibold text-white transition hover:bg-civic-primary-strong disabled:cursor-not-allowed disabled:opacity-60"
              disabled={lookupState === "loading"}
              type="submit"
            >
              <Search className="h-5 w-5" aria-hidden="true" />
              {lookupState === "loading" ? "Searching..." : "Search"}
            </button>
          </form>

          {recentReports.length ? (
            <div className="mt-4 rounded-lg border border-civic-border bg-civic-raised p-3">
              <div className="flex items-center justify-between gap-3">
                <span className="inline-flex items-center gap-2 text-sm font-semibold text-civic-heading">
                  <History className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                  Recent reports on this device
                </span>
                <button
                  className="inline-flex h-9 items-center gap-2 rounded-md border border-civic-border bg-civic-surface px-3 text-xs font-semibold text-civic-muted hover:bg-white hover:text-civic-primary"
                  onClick={clearRecentReports}
                  type="button"
                >
                  <Trash2 className="h-4 w-4" aria-hidden="true" />
                  Clear
                </button>
              </div>
              <div className="mt-3 grid gap-2">
                {recentReports.slice(0, 4).map((report) => (
                  <button
                    className={`grid gap-3 rounded-md border p-3 text-left transition hover:-translate-y-0.5 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center ${
                      backendIncident?.trackingCode === (report.trackingCode ?? report.incidentId)
                        ? "border-civic-primary bg-civic-soft"
                        : "border-civic-border bg-civic-surface hover:border-civic-border-strong"
                    }`}
                    key={report.trackingCode ?? report.incidentId}
                    onClick={() => loadRecentReport(report)}
                    type="button"
                  >
                    <span className="min-w-0">
                      <span className="block break-all text-sm font-semibold text-civic-heading">{formatReportCode(report.trackingCode ?? report.incidentId)}</span>
                      <span className="mt-1 block truncate text-sm text-civic-muted">{compactDescription(report.description)}</span>
                    </span>
                    <span className="flex flex-wrap items-center gap-2 text-xs font-semibold sm:justify-end">
                      <span className="rounded-md bg-civic-soft px-2 py-1 text-civic-primary">{statusLabel(report.status)}</span>
                      <span className="text-civic-muted">{formatSavedDate(report.savedAt)}</span>
                    </span>
                  </button>
                ))}
              </div>
            </div>
          ) : null}

          <div
            className={`mt-4 flex items-start gap-2 rounded-md border p-3 text-sm font-semibold ${
              lookupState === "error"
                ? "border-status-critical bg-status-critical/10 text-status-critical-text"
                : lookupState === "live"
                  ? "border-status-approved bg-status-approved/10 text-status-approved-text"
                  : "border-civic-border bg-civic-raised text-civic-muted"
            }`}
          >
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
            <span>{lookupMessage}</span>
          </div>

          <div className={`mt-3 flex items-start gap-2 rounded-md border p-3 text-sm font-semibold ${realtimeClassName(realtimeState)}`}>
            <RadioTower className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
            <span>
              {realtimeLabel(realtimeState)}: {realtimeMessage}
            </span>
          </div>

          {incident ? (
            <div className="mt-6 rounded-lg border border-civic-border bg-civic-raised p-5">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="break-all font-semibold text-civic-heading">{liveTrackingCode ?? incident.id}</span>
                    <StatusBadge status={incident.status} />
                    <SeverityBadge severity={incident.severity} />
                  </div>
                  <h2 className="mt-3 text-2xl font-semibold text-civic-heading">{incident.title}</h2>
                  <p className="mt-2 text-sm leading-6 text-civic-muted">{incident.aiSummary}</p>
                </div>
                <button
                  className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface px-3 text-sm font-semibold text-civic-primary hover:bg-white"
                  onClick={copyCode}
                  type="button"
                >
                  <Copy className="h-4 w-4" aria-hidden="true" />
                  {copyState}
                </button>
              </div>
              <div className="mt-5">
                <ScoreBar label={backendStatus ? "Backend processing" : "Progress"} score={progress} />
              </div>
            </div>
          ) : (
            <div className="mt-6 rounded-lg border border-civic-border bg-civic-raised p-5 text-sm leading-6 text-civic-muted">
              No report is loaded yet. Submit a report from the citizen portal, then use its tracking code here.
            </div>
          )}
        </Panel>

        <div className="grid gap-6">
          {incident ? (
            <Panel title="Location" description={`Zone ${incident.cityZone}, assigned to ${incident.agency}`}>
              <PublicReportLocationMap
                latitude={incident.coordinates.latitude}
                longitude={incident.coordinates.longitude}
                subtitle={incident.assignedTeam}
                title={incident.location}
              />

              <button
                aria-pressed={alerts}
                className="mt-4 flex w-full items-center justify-between rounded-md border border-civic-border bg-civic-raised p-4 text-left transition hover:bg-civic-soft disabled:cursor-not-allowed disabled:opacity-70"
                disabled={alertPending}
                onClick={toggleAlerts}
                type="button"
              >
                <span className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
                  <Bell className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                  Status alerts
                </span>
                <span className="rounded-md bg-civic-soft px-2 py-1 text-xs font-semibold text-civic-primary">
                  {alertPending ? "Saving" : alerts ? "On" : "Off"}
                </span>
              </button>
              <div className="mt-2 rounded-md border border-civic-border bg-civic-raised px-3 py-2 text-sm font-semibold text-civic-muted">
                {alertState}
              </div>
              <div className="mt-2 rounded-md border border-civic-border bg-civic-raised px-3 py-2 text-xs font-semibold uppercase tracking-[0.12em] text-civic-muted">
                Browser permission: {formatNotificationPermission(notificationPermission)}
              </div>
            </Panel>
          ) : (
            <Panel title="Location" description="Load a live report to see its location and routing.">
              <div className="rounded-md border border-civic-border bg-civic-raised p-5 text-sm leading-6 text-civic-muted">
                Location, agency assignment, alert preferences, and processing status are read from the backend once a valid tracking code is found.
              </div>
            </Panel>
          )}

          {incident ? (
            <PublicAiResultPanel
              duplicates={backendDuplicates}
              incident={incident}
              prediction={backendPrediction}
              status={backendStatus}
            />
          ) : null}
        </div>
      </div>

      <Panel title="Report Timeline" description={activeMilestone?.detail ?? "No processing events are available yet."}>
        <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
          {timeline.map((item) => (
            <button
              aria-pressed={selectedMilestone === item.label}
              className={`rounded-md border p-4 text-left transition hover:-translate-y-0.5 ${
                visibleSelectedMilestone === item.label
                  ? "border-civic-primary bg-civic-soft"
                  : item.complete
                    ? "border-civic-border bg-civic-raised hover:border-civic-border-strong"
                    : "border-civic-border bg-civic-surface hover:bg-civic-raised"
              }`}
              key={item.label}
              onClick={() => setSelectedMilestone(item.label)}
              type="button"
            >
              <item.icon className={`h-5 w-5 ${item.complete ? "text-civic-primary" : "text-civic-muted"}`} aria-hidden="true" />
              <div className="mt-3 flex items-start justify-between gap-3">
                <h3 className="text-sm font-semibold text-civic-heading">{item.label}</h3>
                {item.time ? <span className="shrink-0 text-xs font-semibold text-civic-muted">{item.time}</span> : null}
              </div>
              <p className="mt-2 text-sm leading-6 text-civic-muted">{item.shortDetail}</p>
            </button>
          ))}
          {!timeline.length ? (
            <div className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm font-semibold text-civic-muted md:col-span-4">
              Load a live report to see backend processing milestones.
            </div>
          ) : null}
        </div>
      </Panel>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
        <Panel title="Request An Update" description={requestState}>
          <div className="grid gap-3">
            <label className="block">
              <span className="text-sm font-semibold text-civic-heading">Message</span>
              <textarea
                className={`${fieldClassName} mt-2 min-h-28 resize-none`}
                onChange={(event) => setMessage(event.target.value)}
                value={message}
              />
            </label>
            <button
              className="inline-flex h-12 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-base font-semibold text-white hover:bg-civic-primary-strong disabled:cursor-not-allowed disabled:opacity-70"
              disabled={updateRequestPending}
              onClick={requestUpdate}
              type="button"
            >
              <Send className="h-5 w-5" aria-hidden="true" />
              {updateRequestPending ? "Sending..." : "Send Update Request"}
            </button>
          </div>
        </Panel>

        <Panel title="Experience Feedback" description="Rate the clarity of this report status.">
          <div className="flex gap-2">
            {Array.from({ length: 5 }, (_, index) => {
              const value = index + 1;

              return (
                <button
                  aria-label={`Rate ${value} out of 5`}
                  aria-pressed={rating === value}
                  className={`h-11 w-11 rounded-md border transition ${
                    rating >= value
                      ? "border-civic-primary bg-civic-primary text-white"
                      : "border-civic-border bg-civic-raised text-civic-muted hover:bg-civic-soft"
                  }`}
                  key={value}
                  onClick={() => setRating(value)}
                  type="button"
                >
                  <Star className="mx-auto h-5 w-5" aria-hidden="true" />
                </button>
              );
            })}
          </div>
          <div className="mt-4 rounded-md border border-civic-border bg-civic-raised p-3 text-sm font-semibold text-civic-primary">
            {rating}/5 selected
          </div>
          <button
            className="mt-3 inline-flex h-11 w-full items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-sm font-semibold text-white hover:bg-civic-primary-strong disabled:cursor-not-allowed disabled:opacity-70"
            disabled={feedbackPending}
            onClick={sendFeedback}
            type="button"
          >
            <Send className="h-4 w-4" aria-hidden="true" />
            {feedbackPending ? "Sending..." : "Send Feedback"}
          </button>
          <div className="mt-3 rounded-md border border-civic-border bg-civic-raised p-3 text-sm font-semibold text-civic-muted">
            {feedbackState}
          </div>
        </Panel>
      </div>
    </div>
  );
}

function PublicAiResultPanel({
  duplicates,
  incident,
  prediction,
  status,
}: {
  duplicates: DuplicateCandidateDto[];
  incident: IncidentRow;
  prediction: TriagePredictionDto | null;
  status: IncidentProcessingStatusDto | null;
}) {
  const runningStep = status?.steps.find((step) => !isStepComplete(step.status));

  return (
    <Panel
      title="AI Result"
      description={prediction ? `${Math.round(prediction.confidence * 100)}% confidence from ${prediction.modelName}` : "Waiting for backend AI triage."}
    >
      {prediction ? (
        <div className="grid gap-4">
          <div className="rounded-md border border-civic-border bg-civic-raised p-4">
            <div className="inline-flex items-center gap-2 rounded-md bg-civic-soft px-2 py-1 text-xs font-semibold text-civic-primary">
              <Sparkles className="h-4 w-4" aria-hidden="true" />
              AI-assisted routing
            </div>
            <h3 className="mt-3 text-xl font-semibold text-civic-heading">{prediction.category}</h3>
            <p className="mt-2 text-sm leading-6 text-civic-muted">{prediction.summary}</p>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <PublicAiTile label="Severity" value={prediction.severity} />
            <PublicAiTile label="Agency" value={prediction.suggestedAgencyCode} />
            <PublicAiTile label="Duplicate check" value={duplicates.length ? `${duplicates.length} candidates` : "No close duplicate"} />
            <PublicAiTile label="Status" value={statusLabel(incident.status)} />
          </div>

          <ScoreBar label="AI confidence" score={Math.round(prediction.confidence * 100)} />

          {prediction.evidence.length ? (
            <div className="grid gap-2">
              {prediction.evidence.slice(0, 2).map((item) => (
                <div className="rounded-md border border-civic-border bg-civic-raised p-3" key={item.id}>
                  <div className="flex items-center justify-between gap-3 text-sm">
                    <span className="font-semibold text-civic-heading">{item.title}</span>
                    <span className="text-civic-primary">{Math.round((item.confidence ?? prediction.confidence) * 100)}%</span>
                  </div>
                  <p className="mt-1 text-sm leading-6 text-civic-muted">{item.detail}</p>
                </div>
              ))}
            </div>
          ) : null}
        </div>
      ) : (
        <div className="rounded-md border border-civic-border bg-civic-raised p-4">
          <div className="flex items-start gap-3">
            <Sparkles className="mt-1 h-5 w-5 shrink-0 text-civic-primary" aria-hidden="true" />
            <div>
              <h3 className="font-semibold text-civic-heading">AI triage is pending</h3>
              <p className="mt-2 text-sm leading-6 text-civic-muted">
                The report is saved. Category, severity, agency routing, duplicate signals, and evidence explanations appear here after the worker finishes.
              </p>
            </div>
          </div>
          {runningStep ? (
            <div className="mt-4 rounded-md bg-civic-surface p-3 text-sm font-semibold text-civic-muted">
              Current backend step: {formatStepName(runningStep.name)} ({runningStep.status})
            </div>
          ) : null}
        </div>
      )}
    </Panel>
  );
}

function PublicAiTile({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-raised p-3">
      <div className="text-xs font-semibold uppercase tracking-[0.12em] text-civic-muted">{label}</div>
      <div className="mt-1 break-words text-sm font-semibold text-civic-heading">{value}</div>
    </div>
  );
}

function mapBackendIncident(
  incident: IncidentDto,
  prediction: TriagePredictionDto | null,
  duplicates: DuplicateCandidateDto[],
): IncidentRow {
  const agency = incident.assignedAgencyCode ?? incident.correctedAgencyCode ?? prediction?.suggestedAgencyCode ?? "Pending";
  const category = incident.correctedCategory ?? prediction?.category ?? "PendingTriage";
  const severity = normalizeSeverity(incident.correctedSeverity ?? prediction?.severity);
  const confidence = prediction?.confidence ?? 0;
  const title = `${statusLabel(category)} report`;

  return {
    age: formatAge(incident.createdAt),
    agency,
    aiSummary: prediction?.summary ?? "The backend has received this report and is waiting for the processing pipeline to add predictions.",
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
    duplicates: duplicates.map((candidate) => ({
      caseId: candidate.candidateIncidentId,
      distanceMeters: 0,
      score: candidate.similarityScore,
    })),
    evidence:
      prediction?.evidence.map((item) => ({
        confidence: item.confidence ?? 0,
        detail: item.detail,
        title: item.title,
      })) ?? [],
    id: incident.trackingCode,
    location: `${incident.latitude.toFixed(5)}, ${incident.longitude.toFixed(5)}`,
    reporter: "Citizen portal",
    severity,
    slaRisk: Math.round(Math.max(confidence, 0.45) * 100),
    status: normalizeStatus(incident.status),
    timeline: [],
    title,
  };
}

function normalizeStatus(status: string): IncidentStatus {
  const knownStatuses: IncidentStatus[] = ["Submitted", "Triaged", "HumanReviewRequired", "Approved", "Dispatched"];
  return knownStatuses.includes(status as IncidentStatus) ? (status as IncidentStatus) : "Submitted";
}

function normalizeSeverity(severity?: string | null): Severity {
  const knownSeverities: Severity[] = ["Low", "Medium", "High", "Critical"];
  return knownSeverities.includes(severity as Severity) ? (severity as Severity) : "Medium";
}

function statusProgress(status: string) {
  if (status === "Submitted") {
    return 25;
  }

  if (status === "HumanReviewRequired") {
    return 45;
  }

  if (status === "Triaged") {
    return 62;
  }

  if (status === "Dispatched") {
    return 82;
  }

  return 100;
}

function processingProgress(steps: ProcessingStepDto[]) {
  if (!steps.length) {
    return 25;
  }

  const completed = steps.filter((step) => isStepComplete(step.status)).length;

  return Math.max(25, Math.round((completed / steps.length) * 100));
}

function timelineProgress(items: TimelineItem[]) {
  if (!items.length) {
    return 0;
  }

  return Math.round((items.filter((item) => item.complete).length / items.length) * 100);
}

function buildPublicReportTimeline(
  incident: IncidentDto | null,
  prediction: TriagePredictionDto | null,
  status: IncidentProcessingStatusDto | null,
): TimelineItem[] {
  if (!incident) {
    return [];
  }

  const incidentStatus = normalizeStatus(incident.status);
  const predictionStep = status?.steps.find((step) =>
    ["ai", "analy", "triage", "prediction", "embedding"].some((token) => step.name.toLowerCase().includes(token)),
  );
  const aiComplete = Boolean(prediction) || Boolean(predictionStep && isStepComplete(predictionStep.status)) || incidentStatus !== "Submitted";
  const reviewComplete = Boolean(incident.reviewedAt || incident.reviewDecision);
  const approved = incident.reviewDecision === "Approved" || incidentStatus === "Approved" || incidentStatus === "Dispatched";
  const assigned = Boolean(incident.assignedAt || incident.assignedTeam || incident.assignedAgencyCode);
  const dispatched = Boolean(incident.dispatchedAt) || incidentStatus === "Dispatched";

  return [
    {
      complete: true,
      detail: "The report was received and assigned a public tracking code.",
      icon: CheckCircle2,
      label: "Submitted",
      shortDetail: "Report received",
      time: formatAge(incident.createdAt),
    },
    {
      complete: aiComplete,
      detail: prediction
        ? `${prediction.modelName}${prediction.modelVersion ? ` ${prediction.modelVersion}` : ""} predicted ${prediction.category}, ${prediction.severity}, routed to ${prediction.suggestedAgencyCode}.`
        : predictionStep
          ? `${formatStepName(predictionStep.name)} is ${predictionStep.status.toLowerCase()}.`
          : "The backend worker checks category, severity, agency routing, duplicate risk, and evidence.",
      icon: Sparkles,
      label: "AI triage",
      shortDetail: aiComplete ? "AI result available" : "Worker processing",
      time: prediction ? formatAge(prediction.createdAt) : predictionStep ? formatAge(predictionStep.completedAt ?? predictionStep.startedAt ?? predictionStep.updatedAt) : undefined,
    },
    {
      complete: reviewComplete,
      detail: reviewComplete
        ? incident.reviewNote ?? `Staff review decision recorded: ${incident.reviewDecision ?? "Reviewed"}.`
        : "A reviewer checks the AI result, evidence, duplicate signals, and location before approval.",
      icon: ShieldCheck,
      label: "Review",
      shortDetail: reviewComplete ? "Staff decision recorded" : "Awaiting reviewer",
      time: incident.reviewedAt ? formatAge(incident.reviewedAt) : undefined,
    },
    {
      complete: approved,
      detail: approved ? "The report is approved for agency response." : "Approval happens after review confirms the report is actionable.",
      icon: ClipboardCheck,
      label: "Approved",
      shortDetail: approved ? "Ready for operations" : "Pending approval",
      time: incident.reviewDecision === "Approved" && incident.reviewedAt ? formatAge(incident.reviewedAt) : undefined,
    },
    {
      complete: assigned,
      detail: assigned
        ? `Assigned to ${incident.assignedTeam ?? incident.assignedAgencyCode ?? incident.correctedAgencyCode ?? prediction?.suggestedAgencyCode ?? "agency queue"}.`
        : "Approved reports are assigned to the right agency or crew.",
      icon: UserCheck,
      label: "Assigned",
      shortDetail: assigned ? "Agency owns case" : "Waiting for assignment",
      time: incident.assignedAt ? formatAge(incident.assignedAt) : undefined,
    },
    {
      complete: dispatched,
      detail: dispatched ? "The case was dispatched to field operations." : "Dispatch sends the approved assignment into field response.",
      icon: Truck,
      label: "Dispatched",
      shortDetail: dispatched ? "Field response active" : "Pending dispatch",
      time: incident.dispatchedAt ? formatAge(incident.dispatchedAt) : undefined,
    },
  ];
}

function isStepComplete(status: string) {
  return ["Completed", "Succeeded", "Complete", "Success"].includes(status);
}

function formatStepName(value: string) {
  return statusLabel(value.replace(/[_-]/g, " "));
}

function realtimeLabel(state: RealtimeConnectionState) {
  if (state === "connected") {
    return "Live";
  }

  if (state === "connecting") {
    return "Connecting";
  }

  if (state === "reconnecting") {
    return "Reconnecting";
  }

  if (state === "offline") {
    return "Offline";
  }

  return "Standby";
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

function formatRealtimeTime(value: string) {
  const occurredAt = new Date(value).toLocaleTimeString([], {
    hour: "numeric",
    minute: "2-digit",
  });

  return occurredAt;
}

function formatReportCode(value: string) {
  return value.length > 18 ? `${value.slice(0, 8)}...${value.slice(-6)}` : value;
}

function compactDescription(value: string) {
  const normalized = value.replace(/\s+/g, " ").trim();

  return normalized.length > 92 ? `${normalized.slice(0, 89)}...` : normalized;
}

function formatSavedDate(value: string) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "Recent";
  }

  const day = date.toLocaleDateString([], {
    day: "numeric",
    month: "short",
  });
  const time = date.toLocaleTimeString([], {
    hour: "numeric",
    minute: "2-digit",
  });

  return `${day}, ${time}`;
}

function getApiErrorMessage(error: unknown, fallback: string) {
  return error instanceof CivicApiError ? error.message : fallback;
}

function formatAlertPreferenceState(incident: IncidentDto) {
  if (!incident.notificationAlertsEnabled) {
    return "Status alerts are not enabled for this report.";
  }

  const channel = incident.notificationChannel ?? "Browser";
  const updatedAt = incident.notificationPreferenceUpdatedAt
    ? ` at ${formatRealtimeTime(incident.notificationPreferenceUpdatedAt)}`
    : "";

  return `Alerts enabled through ${channel}${updatedAt}.`;
}

function formatNotificationPermission(permission: BrowserNotificationPermission) {
  if (permission === "granted") {
    return "Allowed";
  }

  if (permission === "denied") {
    return "Blocked";
  }

  if (permission === "default") {
    return "Not asked";
  }

  return "Unsupported";
}

function estimateZone(latitude: number, longitude: number) {
  return (Math.abs(Math.round((latitude + longitude) * 100)) % 30) + 1;
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
