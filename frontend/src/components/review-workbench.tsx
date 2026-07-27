"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  Check,
  ClipboardCheck,
  FileWarning,
  GitMerge,
  History,
  ImageIcon,
  KeyRound,
  Loader2,
  MessageSquareText,
  Mic,
  RefreshCw,
  RotateCcw,
  ShieldAlert,
} from "lucide-react";
import {
  CivicApiError,
  getDuplicateCandidates,
  getIncidentMedia,
  getIncidentReviewHistory,
  getLatestPrediction,
  getOptional,
  login,
  reviewIncident,
  searchIncidents,
  type DuplicateCandidateDto,
  type IncidentDto,
  type IncidentMediaDto,
  type IncidentReviewDto,
  type TriagePredictionDto,
} from "@/lib/civic-api";
import type { EvidenceItem, IncidentRow, IncidentStatus, Severity } from "@/lib/civic-types";
import { fieldClassName, PageHeader, Panel, ScoreBar, SegmentedControl, SeverityBadge, StatusBadge } from "@/components/ui-kit";

const categories = ["RoadDamage", "Drainage", "Sidewalk", "TrafficSignal", "Flooding", "Streetlight", "Sanitation", "GeneralIncident"] as const;
const severities = ["Low", "Medium", "High", "Critical"] as const;
const reviewTabs = ["Evidence", "Correction", "History"] as const;
const tokenStorageKey = "civicsignal-admin-token";
const tokenExpiresAtStorageKey = "civicsignal-admin-token-expires-at";
const tokenRefreshSkewMs = 60_000;

type LoadState = "idle" | "loading" | "ready" | "error";
type ReviewCase = IncidentRow & {
  incident?: IncidentDto;
  prediction?: TriagePredictionDto | null;
  source: "backend";
};

export function ReviewWorkbench() {
  const [cases, setCases] = useState<ReviewCase[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [tab, setTab] = useState<(typeof reviewTabs)[number]>("Evidence");
  const [category, setCategory] = useState<(typeof categories)[number]>("Drainage");
  const [severity, setSeverity] = useState<(typeof severities)[number]>("Medium");
  const [threshold, setThreshold] = useState(78);
  const [decision, setDecision] = useState("Waiting for reviewer decision");
  const [loadState, setLoadState] = useState<LoadState>("idle");
  const [loadMessage, setLoadMessage] = useState("Review queue uses live backend incidents when available.");
  const [accessToken, setAccessToken] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [authState, setAuthState] = useState<LoadState>("idle");
  const [authMessage, setAuthMessage] = useState("Reviewer access is required to submit backend decisions.");
  const [selectedMedia, setSelectedMedia] = useState<IncidentMediaDto[]>([]);
  const [selectedDuplicates, setSelectedDuplicates] = useState<DuplicateCandidateDto[]>([]);
  const [selectedReviews, setSelectedReviews] = useState<IncidentReviewDto[]>([]);
  const [detailsState, setDetailsState] = useState<LoadState>("idle");

  const reviewQueue = useMemo(
    () =>
      cases.filter(
        (incident) =>
          !isFinalReviewQueueStatus(incident.status) &&
          (incident.status === "HumanReviewRequired" ||
            incident.confidence * 100 < threshold ||
            !incident.prediction),
      ),
    [cases, threshold],
  );
  const selectedIncident = reviewQueue.find((incident) => incident.id === selectedId) ?? reviewQueue[0] ?? cases[0];
  const approvalReady = selectedIncident ? selectedIncident.confidence * 100 >= threshold : false;
  const evidenceItems = selectedIncident
    ? selectedIncident.prediction?.evidence.length
      ? selectedIncident.prediction.evidence.map((item) => ({
          confidence: item.confidence ?? selectedIncident.confidence,
          detail: item.detail,
          title: item.title,
        }))
      : selectedIncident.evidence
    : [];

  const loadReviewQueue = useCallback(async (tokenOverride?: string) => {
    const token = tokenOverride ?? (accessToken || undefined);

    setLoadState("loading");
    setLoadMessage("Loading backend incidents and AI predictions...");

    try {
      const apiIncidents = await searchIncidents({ pageSize: 100 }, token);

      if (apiIncidents.length === 0) {
        setCases([]);
        setSelectedId("");
        setLoadState("ready");
        setLoadMessage("No backend incidents currently require reviewer attention.");
        return;
      }

      const mappedCases = await Promise.all(
        apiIncidents.map(async (incident) => {
          const prediction = await getOptional(() => getLatestPrediction(incident.id, token));
          return mapBackendIncidentToReviewCase(incident, prediction);
        }),
      );
      const sortedCases = mappedCases.sort(sortReviewCases);

      setCases(sortedCases);
      setSelectedId((current) => (sortedCases.some((incident) => incident.id === current) ? current : sortedCases[0].id));
      setLoadState("ready");
      setLoadMessage(`${sortedCases.length} backend incidents loaded with AI prediction context.`);
    } catch (error) {
      setCases([]);
      setSelectedId("");
      setLoadState("error");
      setLoadMessage(error instanceof CivicApiError ? error.message : "Backend unavailable. Review queue could not be loaded.");
    }
  }, [accessToken]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      const storedToken = readStoredAccessToken();

      if (storedToken) {
        setAccessToken(storedToken);
        setAuthMessage("Stored reviewer session found.");
      }
      void loadReviewQueue(storedToken ?? undefined);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [loadReviewQueue]);

  useEffect(() => {
    if (!selectedIncident) {
      return;
    }

    const timer = window.setTimeout(() => {
      setCategory(normalizeCategory(selectedIncident.category));
      setSeverity(normalizeSeverity(selectedIncident.severity));
    }, 0);

    return () => window.clearTimeout(timer);
  }, [selectedIncident]);

  useEffect(() => {
    let ignore = false;
    const timer = window.setTimeout(async () => {
      if (!selectedIncident) {
        setSelectedMedia([]);
        setSelectedDuplicates([]);
        setSelectedReviews([]);
        setDetailsState("idle");
        return;
      }

      setDetailsState("loading");

      try {
        const [media, duplicates, reviews] = await Promise.all([
          getOptional(() => getIncidentMedia(selectedIncident.id, accessToken || undefined)),
          getOptional(() => getDuplicateCandidates(selectedIncident.id, accessToken || undefined)),
          getIncidentReviewHistory(selectedIncident.id, accessToken || undefined).catch(() => null),
        ]);

        if (ignore) {
          return;
        }

        setSelectedMedia(media ?? []);
        setSelectedDuplicates(duplicates ?? []);
        setSelectedReviews(reviews ?? []);
        setDetailsState("ready");
      } catch {
        if (!ignore) {
          setSelectedMedia([]);
          setSelectedDuplicates([]);
          setSelectedReviews([]);
          setDetailsState("error");
        }
      }
    }, 0);

    return () => {
      ignore = true;
      window.clearTimeout(timer);
    };
  }, [accessToken, selectedIncident]);

  async function signIn() {
    setAuthState("loading");
    setAuthMessage("Signing in reviewer...");

    try {
      const token = await login(email.trim(), password);
      setAccessToken(token.accessToken);
      storeAccessToken(token.accessToken, token.expiresIn);
      setAuthState("ready");
      setAuthMessage("Reviewer session ready. Decisions can be submitted.");
      await loadReviewQueue(token.accessToken);
    } catch (error) {
      clearStoredAccessToken();
      setAccessToken("");
      setAuthState("error");
      setAuthMessage(error instanceof CivicApiError ? error.message : "Could not sign in reviewer.");
    }
  }

  async function submitDecision(decisionValue: "Approved" | "NeedsMoreInfo", acceptedPrediction: boolean) {
    if (!selectedIncident) {
      setDecision("Select a backend incident before submitting a review decision.");
      return;
    }

    setDecision(`Submitting ${decisionValue.toLowerCase()} decision...`);

    try {
      const updated = await reviewIncident(
        selectedIncident.id,
        {
          acceptedPrediction,
          correctedAgencyCode: selectedIncident.agency,
          correctedCategory: category,
          correctedSeverity: severity,
          decision: decisionValue,
          note: acceptedPrediction
            ? "AI prediction accepted by reviewer."
            : `Reviewer corrected triage to ${category}, ${severity}.`,
        },
        accessToken || undefined,
      );

      const nextCase = mapBackendIncidentToReviewCase(updated, selectedIncident.prediction ?? null);
      setCases((current) => current.map((incident) => (incident.id === selectedIncident.id ? nextCase : incident)));
      setDecision(`${decisionValue} submitted for ${shortId(selectedIncident.id)}.`);
      await loadReviewQueue();
    } catch (error) {
      if (error instanceof CivicApiError && error.status === 401) {
        clearStoredAccessToken();
        setAccessToken("");
        setAuthMessage("Reviewer session expired. Sign in again.");
      }

      setDecision(error instanceof CivicApiError ? error.message : "Review decision could not be submitted.");
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        actions={
          <button
            className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface px-3 text-sm font-semibold text-civic-primary hover:bg-white"
            onClick={() => void loadReviewQueue()}
            type="button"
          >
            <RefreshCw className="h-4 w-4" aria-hidden="true" />
            Refresh
          </button>
        }
        description="Human reviewers can inspect AI evidence, media analysis, duplicate candidates, and submit protected decisions."
        eyebrow="Human Review"
        title="Review Workbench"
      />

      <div
        className={`rounded-lg border p-4 text-sm font-semibold ${
          loadState === "ready"
            ? "border-status-approved bg-status-approved/10 text-status-approved-text"
            : loadState === "loading"
              ? "border-civic-border bg-civic-raised text-civic-muted"
              : "border-status-review bg-status-review/10 text-status-review-text"
        }`}
      >
        {loadState === "loading" ? <Loader2 className="mr-2 inline h-4 w-4 animate-spin" aria-hidden="true" /> : null}
        {loadMessage}
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(300px,0.75fr)_minmax(0,1.25fr)]">
        <div className="grid gap-6">
          <Panel title="Reviewer Access" description={authMessage}>
            {accessToken ? (
              <div className="rounded-md border border-status-approved bg-status-approved/10 p-3 text-sm font-semibold text-status-approved-text">
                Reviewer token is active for protected review actions.
              </div>
            ) : (
              <div className="grid gap-3">
                <input
                  autoComplete="email"
                  className={fieldClassName}
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="reviewer@civicsignal.local"
                  value={email}
                />
                <input
                  autoComplete="current-password"
                  className={fieldClassName}
                  onChange={(event) => setPassword(event.target.value)}
                  placeholder="Password"
                  type="password"
                  value={password}
                />
                <button
                  className="inline-flex h-11 items-center justify-center gap-2 rounded-md bg-civic-primary px-3 text-sm font-semibold text-white transition hover:bg-civic-primary-strong disabled:cursor-not-allowed disabled:opacity-60"
                  disabled={authState === "loading" || !email.trim() || !password}
                  onClick={signIn}
                  type="button"
                >
                  {authState === "loading" ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <KeyRound className="h-4 w-4" aria-hidden="true" />}
                  Sign In
                </button>
              </div>
            )}
          </Panel>

          <Panel title="Review Queue" description={`${reviewQueue.length} cases need reviewer attention.`}>
            <div className="grid max-h-[680px] gap-2 overflow-y-auto pr-1">
              {reviewQueue.map((incident) => (
                <button
                  className={`rounded-md border p-4 text-left transition ${
                    selectedIncident?.id === incident.id
                      ? "border-civic-primary bg-civic-soft"
                      : "border-civic-border bg-civic-raised hover:border-civic-border-strong"
                  }`}
                  key={incident.id}
                  onClick={() => {
                    setSelectedId(incident.id);
                    setDecision("Waiting for reviewer decision");
                    setTab("Evidence");
                  }}
                  type="button"
                >
                  <div className="flex items-center justify-between gap-3">
                    <span className="break-all font-semibold text-civic-heading">{shortId(incident.id)}</span>
                    <SeverityBadge severity={incident.severity} />
                  </div>
                  <p className="mt-2 text-sm font-medium text-civic-ink">{incident.title}</p>
                  <p className="mt-1 text-sm text-civic-muted">Backend AI case</p>
                  <div className="mt-3">
                    <ScoreBar label="Confidence" score={incident.confidence * 100} />
                  </div>
                </button>
              ))}
              {!reviewQueue.length ? (
                <div className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm font-semibold text-civic-muted">
                  No backend cases currently need reviewer attention.
                </div>
              ) : null}
            </div>
          </Panel>
        </div>

        <div className="grid gap-6">
          {selectedIncident ? (
            <>
          <Panel
            action={<SegmentedControl onChange={setTab} options={reviewTabs} value={tab} />}
            title={selectedIncident.title}
            description={selectedIncident.aiSummary}
          >
            <div className="mb-5 flex flex-wrap gap-2">
              <StatusBadge status={selectedIncident.status} />
              <SeverityBadge severity={selectedIncident.severity} />
              <span className="rounded-md bg-civic-soft px-2 py-1 text-xs font-semibold text-civic-primary">{selectedIncident.agency}</span>
              <span className="rounded-md bg-civic-soft px-2 py-1 text-xs font-semibold text-civic-primary">{selectedIncident.prediction?.modelName ?? "No model result yet"}</span>
            </div>

            {tab === "Evidence" ? (
              <div className="grid gap-4">
                <div className="grid gap-3">
                  {evidenceItems.map((item) => (
                    <EvidenceCard item={item} key={`${item.title}-${item.detail}`} />
                  ))}
                </div>

                <div className="grid gap-3 lg:grid-cols-2">
                  <div className="rounded-md border border-civic-border bg-civic-raised p-4">
                    <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
                      <ImageIcon className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                      Media Analysis
                    </div>
                    <div className="mt-3 grid gap-2">
                      {selectedMedia.length ? (
                        selectedMedia.map((media) => (
                          <div className="rounded-md border border-civic-border bg-civic-surface p-3" key={media.id}>
                            <div className="flex items-center justify-between gap-3 text-sm">
                              <span className="break-all font-semibold text-civic-heading">{media.fileName}</span>
                              <span className="text-civic-primary">{media.analysisStatus}</span>
                            </div>
                            <p className="mt-2 text-sm text-civic-muted">{media.analysisSummary ?? media.analysisError ?? "Waiting for media analyzer."}</p>
                            {media.transcript ? (
                              <p className="mt-2 rounded-md bg-civic-soft p-2 text-sm text-civic-primary">
                                <Mic className="mr-1 inline h-4 w-4" aria-hidden="true" />
                                {media.transcript}
                              </p>
                            ) : null}
                          </div>
                        ))
                      ) : (
                        <p className="rounded-md border border-civic-border bg-civic-surface p-3 text-sm text-civic-muted">
                          {detailsState === "loading" ? "Loading media analysis..." : "No backend media evidence found."}
                        </p>
                      )}
                    </div>
                  </div>

                  <div className="rounded-md border border-civic-border bg-civic-raised p-4">
                    <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
                      <GitMerge className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                      Duplicate Candidates
                    </div>
                    <div className="mt-3 grid gap-2">
                      {selectedDuplicates.length ? (
                        selectedDuplicates.map((duplicate) => (
                          <div className="rounded-md border border-civic-border bg-civic-surface p-3" key={duplicate.id}>
                            <div className="flex items-center justify-between gap-3 text-sm">
                              <span className="break-all font-semibold text-civic-heading">{shortId(duplicate.candidateIncidentId)}</span>
                              <span className="text-civic-primary">{Math.round(duplicate.similarityScore * 100)}%</span>
                            </div>
                            <p className="mt-2 text-sm text-civic-muted">{duplicate.reason ?? "Similar report found by vector/geospatial search."}</p>
                          </div>
                        ))
                      ) : (
                        <p className="rounded-md border border-civic-border bg-civic-surface p-3 text-sm text-civic-muted">
                          No duplicate candidates above the threshold.
                        </p>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            ) : null}

            {tab === "Correction" ? (
              <div className="grid gap-5">
                <div>
                  <label className="mb-2 block text-sm font-semibold text-civic-heading">Auto-approval threshold</label>
                  <input
                    className="w-full accent-civic-primary"
                    max="98"
                    min="50"
                    onChange={(event) => setThreshold(Number(event.target.value))}
                    type="range"
                    value={threshold}
                  />
                  <div className="mt-2 flex items-center justify-between text-sm text-civic-muted">
                    <span>50%</span>
                    <span className="font-semibold text-civic-heading">{threshold}%</span>
                    <span>98%</span>
                  </div>
                </div>

                <div className="grid gap-3 lg:grid-cols-2">
                  <label className="block">
                    <span className="mb-2 block text-sm font-semibold text-civic-heading">Corrected category</span>
                    <select className={fieldClassName} onChange={(event) => setCategory(normalizeCategory(event.target.value))} value={category}>
                      {categories.map((option) => (
                        <option key={option} value={option}>
                          {option}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label className="block">
                    <span className="mb-2 block text-sm font-semibold text-civic-heading">Corrected severity</span>
                    <select className={fieldClassName} onChange={(event) => setSeverity(normalizeSeverity(event.target.value))} value={severity}>
                      {severities.map((option) => (
                        <option key={option} value={option}>
                          {option}
                        </option>
                      ))}
                    </select>
                  </label>
                </div>

                <div className="rounded-md border border-civic-border bg-civic-raised p-4">
                  <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
                    {approvalReady ? (
                      <Check className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                    ) : (
                      <ShieldAlert className="h-4 w-4 text-status-review-text" aria-hidden="true" />
                    )}
                    {approvalReady ? "Ready for approval" : "Needs reviewer confirmation"}
                  </div>
                  <p className="mt-2 text-sm leading-6 text-civic-muted">
                    Current confidence is {Math.round(selectedIncident.confidence * 100)}%. Reviewer correction is {category}, {severity}.
                  </p>
                </div>
              </div>
            ) : null}

            {tab === "History" ? (
              <div className="grid gap-3">
                {selectedReviews.length ? (
                  selectedReviews.map((review) => (
                    <div className="rounded-md border border-civic-border bg-civic-raised p-4" key={review.id}>
                      <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
                        <History className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                        {review.decision}
                      </div>
                      <p className="mt-2 text-sm text-civic-muted">
                        {formatDateTime(review.createdAt)}: {review.note ?? "No reviewer note."}
                      </p>
                    </div>
                  ))
                ) : (
                  selectedIncident.timeline.map((event) => (
                    <div className="rounded-md border border-civic-border bg-civic-raised p-4" key={`${event.label}-${event.time}`}>
                      <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
                        <History className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                        {event.label}
                      </div>
                      <p className="mt-2 text-sm text-civic-muted">
                        {event.time}: {event.detail}
                      </p>
                    </div>
                  ))
                )}
              </div>
            ) : null}
          </Panel>

          <Panel
            action={<span className="rounded-md bg-civic-soft px-3 py-1 text-sm font-semibold text-civic-primary">{decision}</span>}
            title="Decision Console"
            description="Reviewer actions write to the backend when signed in."
          >
            <div className="grid gap-3 sm:grid-cols-3">
              <button
                className="inline-flex h-12 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-sm font-semibold text-white hover:bg-civic-primary-strong"
                onClick={() => void submitDecision("Approved", true)}
                type="button"
              >
                <Check className="h-4 w-4" aria-hidden="true" />
                Approve
              </button>
              <button
                className="inline-flex h-12 items-center justify-center gap-2 rounded-md border border-civic-border px-4 text-sm font-semibold text-civic-primary hover:bg-civic-soft"
                onClick={() => void submitDecision("Approved", false)}
                type="button"
              >
                <ClipboardCheck className="h-4 w-4" aria-hidden="true" />
                Correct
              </button>
              <button
                className="inline-flex h-12 items-center justify-center gap-2 rounded-md border border-civic-border px-4 text-sm font-semibold text-status-critical-text hover:bg-status-critical"
                onClick={() => void submitDecision("NeedsMoreInfo", false)}
                type="button"
              >
                <FileWarning className="h-4 w-4" aria-hidden="true" />
                More Evidence
              </button>
            </div>
            <button
              className="mt-4 inline-flex h-10 items-center gap-2 rounded-md px-3 text-sm font-semibold text-civic-muted hover:bg-civic-soft hover:text-civic-primary"
              onClick={() => setDecision("Waiting for reviewer decision")}
              type="button"
            >
              <RotateCcw className="h-4 w-4" aria-hidden="true" />
              Reset decision
            </button>
          </Panel>

          {!approvalReady ? (
            <div className="rounded-lg border border-status-review bg-status-review p-4 text-sm text-status-review-text">
              <div className="flex items-center gap-2 font-semibold">
                <AlertTriangle className="h-4 w-4" aria-hidden="true" />
                Confidence is below the current approval threshold.
              </div>
            </div>
          ) : null}
            </>
          ) : (
            <Panel title="No Case Selected" description="The review workbench is connected to backend incidents only.">
              <div className="rounded-md border border-civic-border bg-civic-raised p-5 text-sm leading-6 text-civic-muted">
                Create a report that requires human review, run the processing worker, or adjust the review threshold to populate this queue.
              </div>
            </Panel>
          )}
        </div>
      </div>
    </div>
  );
}

function EvidenceCard({ item }: { item: EvidenceItem }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-raised p-4">
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
          <MessageSquareText className="h-4 w-4 text-civic-primary" aria-hidden="true" />
          {item.title}
        </div>
        <span className="text-sm font-semibold text-civic-primary">{Math.round(item.confidence * 100)}%</span>
      </div>
      <p className="mt-2 text-sm leading-6 text-civic-muted">{item.detail}</p>
    </div>
  );
}

function mapBackendIncidentToReviewCase(incident: IncidentDto, prediction: TriagePredictionDto | null): ReviewCase {
  const category = incident.correctedCategory ?? prediction?.category ?? inferCategory(incident.description);
  const severity = normalizeSeverity(incident.correctedSeverity ?? prediction?.severity ?? inferSeverity(incident.description));
  const agency = incident.correctedAgencyCode ?? prediction?.suggestedAgencyCode ?? inferAgency(category);
  const status = normalizeStatus(incident.status);
  const confidence = prediction?.confidence ?? (incident.acceptedPrediction ? 0.86 : 0.58);

  return {
    age: formatAge(incident.createdAt),
    agency,
    aiSummary: prediction?.summary ?? "AI triage is pending or unavailable for this backend incident.",
    assignedTeam: `${agency} review queue`,
    category,
    channel: "Web",
    cityZone: estimateZone(incident.latitude, incident.longitude),
    confidence,
    coordinates: {
      latitude: incident.latitude,
      longitude: incident.longitude,
    },
    description: incident.description,
    duplicates: incident.duplicateOfIncidentId
      ? [
          {
            caseId: incident.duplicateOfIncidentId,
            distanceMeters: 0,
            score: 0.9,
          },
        ]
      : [],
    evidence: prediction?.evidence.length
      ? prediction.evidence.map((item) => ({
          confidence: item.confidence ?? confidence,
          detail: item.detail,
          title: item.title,
        }))
      : [
          {
            confidence,
            detail: "Prediction has not completed yet, or the AI service used fallback routing.",
            title: "AI status",
          },
        ],
    id: incident.id,
    incident,
    location: `${incident.latitude.toFixed(5)}, ${incident.longitude.toFixed(5)}`,
    prediction,
    reporter: "Citizen portal",
    severity,
    slaRisk: calculateSlaRisk(status, severity, confidence),
    source: "backend",
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
              detail: `${prediction.modelName} produced ${prediction.category} at ${Math.round(prediction.confidence * 100)}% confidence.`,
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
    ],
    title: `${formatCategoryTitle(category)} report`,
  };
}

function sortReviewCases(left: ReviewCase, right: ReviewCase) {
  const leftPriority = reviewPriority(left);
  const rightPriority = reviewPriority(right);

  return rightPriority - leftPriority || right.slaRisk - left.slaRisk;
}

function reviewPriority(incident: ReviewCase) {
  if (incident.status === "HumanReviewRequired") {
    return 4;
  }

  if (!incident.prediction) {
    return 3;
  }

  if (incident.confidence < 0.75) {
    return 2;
  }

  return 1;
}

function isFinalReviewQueueStatus(status: IncidentStatus) {
  return status === "Approved" || status === "Dispatched";
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

function normalizeCategory(value: string): (typeof categories)[number] {
  return categories.includes(value as (typeof categories)[number])
    ? (value as (typeof categories)[number])
    : "GeneralIncident";
}

function normalizeSeverity(value?: string | null): Severity {
  return severities.includes(value as Severity) ? (value as Severity) : "Medium";
}

function normalizeStatus(status: string): IncidentStatus {
  const knownStatuses: IncidentStatus[] = ["Submitted", "Triaged", "HumanReviewRequired", "Approved", "Dispatched"];
  return knownStatuses.includes(status as IncidentStatus) ? (status as IncidentStatus) : "Submitted";
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

  if (lower.includes("hazard") || lower.includes("flood") || lower.includes("sinkhole")) {
    return "Critical";
  }

  if (lower.includes("dark") || lower.includes("outage") || lower.includes("swerving") || lower.includes("blocked") || lower.includes("large")) {
    return "High";
  }

  return "Medium";
}

function inferAgency(category: string) {
  if (category === "RoadDamage" || category === "TrafficSignal") {
    return "DOT";
  }

  if (category === "Drainage" || category === "Sidewalk" || category === "Flooding") {
    return "DPW";
  }

  if (category === "Streetlight") {
    return "UTILITIES";
  }

  if (category === "Sanitation") {
    return "SANITATION";
  }

  return "CITYOPS";
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

  return Math.min(100, Math.max(10, Math.round(severityBase[severity] + statusAdjustment + (1 - confidence) * 18)));
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

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

function formatCategoryTitle(value: string) {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function shortId(value: string) {
  return value.length > 18 ? `${value.slice(0, 8)}...${value.slice(-6)}` : value;
}
