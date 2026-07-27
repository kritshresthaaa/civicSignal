"use client";

import Link from "next/link";
import type { FormEvent, ReactNode } from "react";
import { useCallback, useEffect, useMemo, useState } from "react";
import {
  ArrowLeft,
  Bell,
  CheckCircle2,
  Clock,
  FileImage,
  ImageIcon,
  MapPin,
  MessageCircle,
  RadioTower,
  RefreshCw,
  Send,
  Sparkles,
  ThumbsUp,
} from "lucide-react";
import {
  CivicApiError,
  getOptional,
  getPublicDuplicateCandidates,
  getPublicIncidentFeedback,
  getPublicIncident,
  getPublicIncidentMedia,
  getPublicIncidentStatus,
  getPublicLatestPrediction,
  requestPublicIncidentUpdate,
  searchPublicIncidents,
  submitPublicIncidentFeedback,
  updatePublicNotificationPreference,
  type DuplicateCandidateDto,
  type IncidentDto,
  type IncidentFeedbackDto,
  type IncidentMediaDto,
  type IncidentProcessingStatusDto,
  type PublicIncidentFeedItemDto,
  type TriagePredictionDto,
} from "@/lib/civic-api";
import type { IncidentStatus, Severity } from "@/lib/civic-types";
import { fieldClassName, Panel, ScoreBar, SeverityBadge, StatusBadge } from "@/components/ui-kit";

export function PublicIncidentDetail({ trackingCode }: { trackingCode: string }) {
  const [incident, setIncident] = useState<IncidentDto | null>(null);
  const [status, setStatus] = useState<IncidentProcessingStatusDto | null>(null);
  const [prediction, setPrediction] = useState<TriagePredictionDto | null>(null);
  const [media, setMedia] = useState<IncidentMediaDto[]>([]);
  const [duplicates, setDuplicates] = useState<DuplicateCandidateDto[]>([]);
  const [feedback, setFeedback] = useState<IncidentFeedbackDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [comment, setComment] = useState("");
  const [actionState, setActionState] = useState("Public actions are ready.");
  const [pendingAction, setPendingAction] = useState<string | null>(null);

  const load = useCallback(async ({ silent = false }: { silent?: boolean } = {}) => {
    if (!silent) {
      setLoading(true);
    }

    setRefreshing(true);

    try {
      const [
        loadedIncident,
        loadedStatus,
        loadedPrediction,
        loadedMedia,
        loadedDuplicates,
        loadedFeedback,
      ] = await Promise.all([
        getPublicIncidentWithFeedFallback(trackingCode),
        getPublicDetailOptional(() => getPublicIncidentStatus(trackingCode)),
        getPublicDetailOptional(() => getPublicLatestPrediction(trackingCode)),
        getPublicDetailOptional(() => getPublicIncidentMedia(trackingCode)),
        getPublicDetailOptional(() => getPublicDuplicateCandidates(trackingCode)),
        getPublicDetailOptional(() => getPublicIncidentFeedback(trackingCode)),
      ]);

      setIncident(loadedIncident);
      setStatus(loadedStatus);
      setPrediction(loadedPrediction);
      setMedia(loadedMedia ?? []);
      setDuplicates(loadedDuplicates ?? []);
      setFeedback(loadedFeedback ?? []);
      setError(null);
    } catch (loadError) {
      setError(loadError instanceof CivicApiError ? loadError.message : "Could not load this public report.");
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [trackingCode]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void load();
    }, 0);

    return () => window.clearTimeout(timeoutId);
  }, [load]);

  const statusValue = normalizeStatus(incident?.status);
  const severityValue = normalizeSeverity(prediction?.severity);
  const publicTimeline = useMemo(() => buildTimeline(incident, status, prediction), [incident, prediction, status]);
  const supportCount = feedback.filter((item) => item.rating >= 5).length;
  const comments = feedback.filter((item) => item.comment?.trim());
  const imageMedia = media.filter((item) => item.mediaType === "Image");
  const audioMedia = media.filter((item) => item.mediaType === "Audio");

  async function handleAlsoSeeThis() {
    setPendingAction("also-see");
    try {
      const created = await submitPublicIncidentFeedback(trackingCode, {
        comment: null,
        rating: 5,
      });
      setFeedback((current) => [created, ...current]);
      setActionState("Thanks. Your confirmation was added to this public report.");
    } catch (actionError) {
      setActionState(actionError instanceof CivicApiError ? actionError.message : "Could not add your confirmation.");
    } finally {
      setPendingAction(null);
    }
  }

  async function handleFollow() {
    if (!incident) {
      return;
    }

    const nextValue = !(incident.notificationAlertsEnabled ?? false);
    setPendingAction("follow");
    try {
      const preference = await updatePublicNotificationPreference(trackingCode, {
        alertsEnabled: nextValue,
        channel: "Browser",
      });
      setIncident((current) =>
        current
          ? {
              ...current,
              notificationAlertsEnabled: preference.alertsEnabled,
              notificationChannel: preference.channel,
              notificationPreferenceUpdatedAt: preference.updatedAt,
            }
          : current,
      );
      setActionState(preference.alertsEnabled ? "This report is now followed in your browser session." : "Follow alerts were turned off.");
    } catch (actionError) {
      setActionState(actionError instanceof CivicApiError ? actionError.message : "Could not update follow preference.");
    } finally {
      setPendingAction(null);
    }
  }

  async function handleRequestUpdate() {
    setPendingAction("update");
    try {
      await requestPublicIncidentUpdate(trackingCode, {
        message: "Please share the next public status update for this report.",
      });
      setActionState("Your update request was sent to the operations team.");
    } catch (actionError) {
      setActionState(actionError instanceof CivicApiError ? actionError.message : "Could not request an update.");
    } finally {
      setPendingAction(null);
    }
  }

  async function handleCommentSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmed = comment.trim();
    if (!trimmed) {
      setActionState("Write a short comment before posting.");
      return;
    }

    setPendingAction("comment");
    try {
      const created = await submitPublicIncidentFeedback(trackingCode, {
        comment: trimmed,
        rating: 4,
      });
      setFeedback((current) => [created, ...current]);
      setComment("");
      setActionState("Your public comment was added.");
    } catch (actionError) {
      setActionState(actionError instanceof CivicApiError ? actionError.message : "Could not post your comment.");
    } finally {
      setPendingAction(null);
    }
  }

  if (loading) {
    return <DetailSkeleton />;
  }

  if (error || !incident) {
    return (
      <Panel title="Report Not Found" description={error ?? "This tracking code does not match a public report."}>
        <Link className="inline-flex h-11 items-center gap-2 rounded-md bg-civic-primary px-4 text-sm font-semibold text-white" href="/public/incidents">
          <ArrowLeft className="h-4 w-4" aria-hidden="true" />
          Back to Feed
        </Link>
      </Panel>
    );
  }

  return (
    <div className="space-y-6">
      <section className="relative overflow-hidden rounded-lg border border-civic-border bg-civic-surface p-5 shadow-sm lg:p-6">
        <div className="absolute inset-0 opacity-70 [background-image:linear-gradient(rgba(42,129,110,0.08)_1px,transparent_1px),linear-gradient(90deg,rgba(42,129,110,0.08)_1px,transparent_1px)] [background-size:42px_42px]" />
        <div className="relative">
          <Link className="inline-flex items-center gap-2 text-sm font-semibold text-civic-primary hover:text-civic-primary-strong" href="/public/incidents">
            <ArrowLeft className="h-4 w-4" aria-hidden="true" />
            Back to public feed
          </Link>
          <div className="mt-5 grid gap-5 lg:grid-cols-[minmax(0,1fr)_360px]">
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <StatusBadge status={statusValue} />
                <SeverityBadge severity={severityValue} />
                <span className="rounded-md bg-civic-soft px-2 py-1 text-xs font-semibold text-civic-primary">{trackingCode}</span>
              </div>
              <h1 className="mt-4 max-w-3xl text-4xl font-semibold leading-tight text-civic-heading">
                {prediction?.category ? formatCategory(prediction.category) : "Public Report"}
              </h1>
              <p className="mt-3 max-w-3xl text-base leading-7 text-civic-muted">{incident.description}</p>
              <div className="mt-4 flex flex-wrap gap-3 text-sm font-semibold text-civic-muted">
                <span className="inline-flex items-center gap-2">
                  <MapPin className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                  {incident.latitude.toFixed(5)}, {incident.longitude.toFixed(5)}
                </span>
                <span className="inline-flex items-center gap-2">
                  <Clock className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                  Reported {formatDateTime(incident.createdAt)}
                </span>
              </div>
            </div>
            <div className="rounded-lg border border-civic-border bg-civic-surface/95 p-4 shadow-sm backdrop-blur">
              <p className="text-sm font-semibold text-civic-heading">Community Actions</p>
              <p className="mt-1 text-xs leading-5 text-civic-muted">{actionState}</p>
              <div className="mt-4 grid gap-2">
                <ActionButton
                  active={pendingAction === "also-see"}
                  icon={<ThumbsUp className="h-4 w-4" aria-hidden="true" />}
                  label={`I also see this (${supportCount})`}
                  onClick={() => void handleAlsoSeeThis()}
                />
                <ActionButton
                  active={pendingAction === "follow"}
                  icon={<Bell className="h-4 w-4" aria-hidden="true" />}
                  label={incident.notificationAlertsEnabled ? "Following updates" : "Follow updates"}
                  onClick={() => void handleFollow()}
                />
                <ActionButton
                  active={pendingAction === "update"}
                  icon={<RadioTower className="h-4 w-4" aria-hidden="true" />}
                  label="Request update"
                  onClick={() => void handleRequestUpdate()}
                />
                <button
                  className="inline-flex h-11 items-center justify-center gap-2 rounded-md border border-civic-border px-4 text-sm font-semibold text-civic-primary transition hover:bg-civic-soft disabled:opacity-60"
                  disabled={refreshing}
                  onClick={() => void load({ silent: true })}
                  type="button"
                >
                  <RefreshCw className={`h-4 w-4 ${refreshing ? "animate-spin" : ""}`} aria-hidden="true" />
                  Refresh
                </button>
              </div>
            </div>
          </div>
        </div>
      </section>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_390px]">
        <main className="space-y-6">
          <MediaGallery imageMedia={imageMedia} audioMedia={audioMedia} />
          <AiSummary prediction={prediction} duplicates={duplicates} />
          <PublicComments
            comments={comments}
            comment={comment}
            onChange={setComment}
            onSubmit={handleCommentSubmit}
            pending={pendingAction === "comment"}
          />
        </main>

        <aside className="space-y-6 xl:sticky xl:top-28 xl:self-start">
          <TimelinePanel timeline={publicTimeline} />
          <MapPanel incident={incident} />
          <DuplicatePanel duplicates={duplicates} />
        </aside>
      </div>
    </div>
  );
}

function MediaGallery({ imageMedia, audioMedia }: { imageMedia: IncidentMediaDto[]; audioMedia: IncidentMediaDto[] }) {
  return (
    <Panel
      action={<span className="rounded-md bg-civic-soft px-3 py-2 text-xs font-semibold text-civic-primary">{imageMedia.length + audioMedia.length} media</span>}
      description="Citizen-submitted evidence and AI media analysis results."
      title="Evidence Gallery"
    >
      {imageMedia.length ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {imageMedia.map((item) => {
            const imageUrl = resolvePublicMediaUrl(item.storageUri);
            return (
              <div className="overflow-hidden rounded-lg border border-civic-border bg-civic-raised" key={item.id}>
                {imageUrl ? (
                  <>
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img alt={item.fileName} className="h-72 w-full bg-black object-cover" loading="lazy" src={imageUrl} />
                  </>
                ) : (
                  <div className="grid h-72 place-items-center text-civic-muted">
                    <ImageIcon className="h-8 w-8" aria-hidden="true" />
                  </div>
                )}
                <div className="p-3">
                  <p className="truncate text-sm font-semibold text-civic-heading">{item.fileName}</p>
                  <p className="mt-1 text-xs font-semibold text-civic-muted">{item.analysisSummary ?? item.analysisStatus}</p>
                </div>
              </div>
            );
          })}
        </div>
      ) : (
        <div className="rounded-lg border border-dashed border-civic-border bg-civic-raised p-6 text-center">
          <FileImage className="mx-auto h-8 w-8 text-civic-primary" aria-hidden="true" />
          <p className="mt-3 text-sm font-semibold text-civic-heading">No public photo evidence yet</p>
          <p className="mt-1 text-sm text-civic-muted">The report can still be triaged from text and location.</p>
        </div>
      )}

      {audioMedia.length ? (
        <div className="mt-4 grid gap-3">
          {audioMedia.map((item) => {
            const audioUrl = resolvePublicMediaUrl(item.storageUri);
            return (
              <div className="rounded-lg border border-civic-border bg-civic-raised p-4" key={item.id}>
                <p className="text-sm font-semibold text-civic-heading">{item.fileName}</p>
                {audioUrl ? <audio className="mt-3 w-full" controls src={audioUrl} /> : null}
                {item.transcript ? <p className="mt-3 rounded-md bg-civic-surface p-3 text-sm leading-6 text-civic-muted">{item.transcript}</p> : null}
              </div>
            );
          })}
        </div>
      ) : null}
    </Panel>
  );
}

function AiSummary({ prediction, duplicates }: { prediction: TriagePredictionDto | null; duplicates: DuplicateCandidateDto[] }) {
  return (
    <Panel
      action={<Sparkles className="h-5 w-5 text-civic-primary" aria-hidden="true" />}
      description="Public explanation of how the backend understands this report."
      title="AI and Routing Result"
    >
      {prediction ? (
        <div className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-3">
            <Insight label="Category" value={formatCategory(prediction.category)} />
            <Insight label="Agency" value={prediction.suggestedAgencyCode} />
            <Insight label="Duplicates" value={String(duplicates.length)} />
          </div>
          <ScoreBar label="AI confidence" score={prediction.confidence * 100} />
          <p className="rounded-md bg-civic-soft p-4 text-sm leading-6 text-civic-muted">{prediction.summary}</p>
          {prediction.evidence.length ? (
            <div className="grid gap-2">
              {prediction.evidence.slice(0, 5).map((item) => (
                <div className="rounded-md border border-civic-border bg-civic-raised p-3" key={item.id}>
                  <p className="text-sm font-semibold text-civic-heading">{item.title}</p>
                  <p className="mt-1 text-xs leading-5 text-civic-muted">{item.detail}</p>
                </div>
              ))}
            </div>
          ) : null}
        </div>
      ) : (
        <p className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm text-civic-muted">
          AI triage has not completed yet. Refresh or follow this report for updates.
        </p>
      )}
    </Panel>
  );
}

function PublicComments({
  comment,
  comments,
  onChange,
  onSubmit,
  pending,
}: {
  comment: string;
  comments: IncidentFeedbackDto[];
  onChange: (value: string) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  pending: boolean;
}) {
  return (
    <Panel
      action={<span className="rounded-md bg-civic-soft px-3 py-2 text-xs font-semibold text-civic-primary">{comments.length} comments</span>}
      description="Residents can add helpful public context without exposing internal staff notes."
      title="Community Notes"
    >
      <form className="rounded-lg border border-civic-border bg-civic-raised p-4" id="comments" onSubmit={onSubmit}>
        <label className="text-sm font-semibold text-civic-heading" htmlFor="public-comment">Add public context</label>
        <textarea
          className={`${fieldClassName} mt-2 min-h-28 resize-y`}
          id="public-comment"
          maxLength={2000}
          onChange={(event) => onChange(event.target.value)}
          placeholder="Add a safe public note, for example: the issue is blocking the right lane."
          value={comment}
        />
        <div className="mt-3 flex justify-end">
          <button
            className="inline-flex h-11 items-center gap-2 rounded-md bg-civic-primary px-4 text-sm font-semibold text-white transition hover:bg-civic-primary-strong disabled:cursor-not-allowed disabled:opacity-60"
            disabled={pending}
            type="submit"
          >
            <Send className="h-4 w-4" aria-hidden="true" />
            Post Comment
          </button>
        </div>
      </form>

      <div className="mt-4 space-y-3">
        {comments.length ? (
          comments.map((item) => (
            <article className="rounded-lg border border-civic-border bg-civic-raised p-4" key={item.id}>
              <div className="flex items-center justify-between gap-3">
                <p className="inline-flex items-center gap-2 text-sm font-semibold text-civic-heading">
                  <MessageCircle className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                  Resident note
                </p>
                <span className="text-xs font-semibold text-civic-muted">{formatDateTime(item.createdAt)}</span>
              </div>
              <p className="mt-3 text-sm leading-6 text-civic-muted">{item.comment}</p>
            </article>
          ))
        ) : (
          <p className="rounded-lg border border-dashed border-civic-border bg-civic-raised p-5 text-sm text-civic-muted">
            No public comments yet.
          </p>
        )}
      </div>
    </Panel>
  );
}

function TimelinePanel({ timeline }: { timeline: Array<{ complete: boolean; detail: string; label: string }> }) {
  return (
    <Panel title="Public Timeline" description="High-level status only. Internal staff notes stay private.">
      <div className="space-y-3">
        {timeline.map((item) => (
          <div className="flex gap-3 rounded-md border border-civic-border bg-civic-raised p-3" key={item.label}>
            <span className={`mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full ${item.complete ? "bg-civic-primary text-white" : "bg-civic-soft text-civic-primary"}`}>
              <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
            </span>
            <div>
              <p className="text-sm font-semibold text-civic-heading">{item.label}</p>
              <p className="mt-1 text-xs leading-5 text-civic-muted">{item.detail}</p>
            </div>
          </div>
        ))}
      </div>
    </Panel>
  );
}

function MapPanel({ incident }: { incident: IncidentDto }) {
  return (
    <Panel title="Approximate Area" description="The public view uses report coordinates for context.">
      <div className="relative h-64 overflow-hidden rounded-lg border border-civic-border bg-[#eef6f3]">
        <div className="absolute inset-0 [background-image:linear-gradient(rgba(42,129,110,0.12)_1px,transparent_1px),linear-gradient(90deg,rgba(42,129,110,0.12)_1px,transparent_1px)] [background-size:38px_38px]" />
        <div className="absolute left-[-18%] top-[52%] h-7 w-[140%] rotate-[-10deg] bg-white/70" />
        <div className="absolute left-[56%] top-[-10%] h-[125%] w-6 rotate-[4deg] bg-white/75" />
        <div className="absolute left-1/2 top-1/2 flex h-16 w-16 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full border-4 border-white bg-civic-primary text-white shadow-lg">
          <MapPin className="h-7 w-7" aria-hidden="true" />
        </div>
        <div className="absolute bottom-3 left-3 right-3 rounded-md bg-civic-surface/95 p-3 text-xs font-semibold text-civic-muted shadow-sm">
          {incident.latitude.toFixed(5)}, {incident.longitude.toFixed(5)}
        </div>
      </div>
    </Panel>
  );
}

function DuplicatePanel({ duplicates }: { duplicates: DuplicateCandidateDto[] }) {
  return (
    <Panel title="Similar Reports" description="Possible duplicate or nearby related reports.">
      <div className="space-y-2">
        {duplicates.length ? (
          duplicates.slice(0, 5).map((item) => (
            <div className="rounded-md border border-civic-border bg-civic-raised p-3" key={item.id}>
              <div className="flex items-center justify-between gap-3 text-sm">
                <span className="font-mono font-semibold text-civic-heading">{shortId(item.candidateIncidentId)}</span>
                <span className="font-semibold text-civic-primary">{Math.round(item.similarityScore * 100)}%</span>
              </div>
              <p className="mt-2 text-xs leading-5 text-civic-muted">{item.reason ?? "Similar text and nearby coordinates."}</p>
            </div>
          ))
        ) : (
          <p className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm text-civic-muted">
            No duplicate candidates are currently attached to this report.
          </p>
        )}
      </div>
    </Panel>
  );
}

function ActionButton({ active, icon, label, onClick }: { active: boolean; icon: ReactNode; label: string; onClick: () => void }) {
  return (
    <button
      className="inline-flex h-11 items-center justify-center gap-2 rounded-md border border-civic-border px-4 text-sm font-semibold text-civic-primary transition hover:bg-civic-soft disabled:cursor-not-allowed disabled:opacity-60"
      disabled={active}
      onClick={onClick}
      type="button"
    >
      {active ? <RefreshCw className="h-4 w-4 animate-spin" aria-hidden="true" /> : icon}
      {label}
    </button>
  );
}

function Insight({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-raised p-3">
      <p className="text-xs font-semibold uppercase text-civic-muted">{label}</p>
      <p className="mt-1 truncate text-sm font-semibold text-civic-heading">{value}</p>
    </div>
  );
}

function DetailSkeleton() {
  return (
    <div className="space-y-4">
      <div className="animate-pulse rounded-lg border border-civic-border bg-civic-surface p-6">
        <div className="h-5 w-32 rounded-md bg-civic-border" />
        <div className="mt-5 h-10 w-2/3 rounded-md bg-civic-border" />
        <div className="mt-4 h-4 w-full rounded-md bg-civic-border" />
        <div className="mt-2 h-4 w-1/2 rounded-md bg-civic-border" />
      </div>
      <div className="grid gap-4 lg:grid-cols-2">
        <div className="h-80 animate-pulse rounded-lg border border-civic-border bg-civic-surface" />
        <div className="h-80 animate-pulse rounded-lg border border-civic-border bg-civic-surface" />
      </div>
    </div>
  );
}

async function getPublicIncidentWithFeedFallback(trackingCode: string) {
  try {
    return await getPublicIncident(trackingCode);
  } catch (error) {
    if (!(error instanceof CivicApiError) || (error.status !== 404 && error.status !== 405)) {
      throw error;
    }

    const feedItems = await searchPublicIncidents({ pageSize: 50 });
    const feedItem = feedItems.find((item) => item.trackingCode.toUpperCase() === trackingCode.toUpperCase());

    if (!feedItem) {
      throw error;
    }

    return incidentFromFeedItem(feedItem);
  }
}

async function getPublicDetailOptional<T>(loader: () => Promise<T>) {
  try {
    return await getOptional(loader);
  } catch (error) {
    if (error instanceof CivicApiError && error.status === 405) {
      return null;
    }

    throw error;
  }
}

function incidentFromFeedItem(item: PublicIncidentFeedItemDto): IncidentDto {
  return {
    assignedAgencyCode: item.agencyCode,
    createdAt: item.createdAt,
    description: item.description,
    id: item.trackingCode,
    latitude: item.approximateLatitude,
    longitude: item.approximateLongitude,
    reviewDecision: item.hasReview ? "Reviewed" : null,
    status: item.status,
    trackingCode: item.trackingCode,
  };
}

function buildTimeline(incident: IncidentDto | null, status: IncidentProcessingStatusDto | null, prediction: TriagePredictionDto | null) {
  const normalizedStatus = normalizeStatus(incident?.status);
  const hasProcessing = Boolean(status?.steps.some((step) => step.status === "Succeeded"));

  return [
    {
      complete: Boolean(incident),
      detail: incident ? `Received ${formatDateTime(incident.createdAt)}.` : "Waiting for report.",
      label: "Submitted",
    },
    {
      complete: hasProcessing || Boolean(prediction),
      detail: prediction ? `${formatCategory(prediction.category)} with ${Math.round(prediction.confidence * 100)}% confidence.` : "AI is checking text, media, location, and duplicates.",
      label: "AI triage",
    },
    {
      complete: Boolean(incident?.reviewDecision) || normalizedStatus === "Approved" || normalizedStatus === "Dispatched",
      detail: incident?.reviewDecision ? `Staff decision: ${incident.reviewDecision}.` : "Staff can approve, correct, or request more information.",
      label: "Staff review",
    },
    {
      complete: Boolean(incident?.assignedAt) || normalizedStatus === "Dispatched",
      detail: incident?.assignedTeam ? `Assigned to ${incident.assignedTeam}.` : "Approved reports move to an agency queue.",
      label: "Agency assignment",
    },
    {
      complete: normalizedStatus === "Dispatched",
      detail: normalizedStatus === "Dispatched" ? "Dispatched for field response." : "Dispatch happens after approval and assignment.",
      label: "Field response",
    },
  ];
}

function normalizeStatus(value: string | null | undefined): IncidentStatus {
  const allowed: readonly IncidentStatus[] = ["Submitted", "Triaged", "HumanReviewRequired", "Approved", "Dispatched"];
  return allowed.includes(value as IncidentStatus) ? (value as IncidentStatus) : "Submitted";
}

function normalizeSeverity(value: string | null | undefined): Severity {
  const allowed: readonly Severity[] = ["Low", "Medium", "High", "Critical"];
  return allowed.includes(value as Severity) ? (value as Severity) : "Medium";
}

function resolvePublicMediaUrl(value: string | null | undefined) {
  if (!value) {
    return null;
  }

  if (value.startsWith("/")) {
    return value;
  }

  try {
    const url = new URL(value);
    if (url.hostname === "localhost" || url.hostname === "127.0.0.1" || url.hostname === "::1") {
      return `${url.pathname}${url.search}`;
    }

    return value;
  } catch {
    return value;
  }
}

function formatCategory(value: string) {
  return value
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/[_-]+/g, " ")
    .trim();
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    month: "short",
  }).format(new Date(value));
}

function shortId(value: string) {
  return `${value.slice(0, 8)}...${value.slice(-6)}`;
}
