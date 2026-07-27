"use client";

import Link from "next/link";
import type { ChangeEvent, DragEvent } from "react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { LucideIcon } from "lucide-react";
import {
  Camera,
  CheckCircle2,
  ClipboardList,
  CloudUpload,
  Copy,
  Droplets,
  Lightbulb,
  LocateFixed,
  MapPin,
  Mic,
  Navigation,
  Pause,
  Play,
  RefreshCw,
  Road,
  RotateCcw,
  Search,
  ShieldCheck,
  Sparkles,
  TrafficCone,
  Trash2,
  Upload,
  WifiOff,
} from "lucide-react";
import { analyzeDraft } from "@/lib/civic-analysis";
import {
  CivicApiError,
  createIncident,
  getOptional,
  getPublicDuplicateCandidates,
  getPublicIncidentStatus,
  getPublicLatestPrediction,
  reverseGeocode,
  searchLocations,
  uploadPublicIncidentMedia,
  type DuplicateCandidateDto,
  type GeocodingResultDto,
  type IncidentDto,
  type IncidentProcessingStatusDto,
  type IncidentMediaDto,
  type TriagePredictionDto,
} from "@/lib/civic-api";
import { createStoredPublicReport, savePublicReport } from "@/lib/public-report-history";
import {
  enqueuePublicReport,
  listQueuedPublicReports,
  subscribeOfflineReportQueue,
  syncQueuedPublicReports,
  type QueuedPublicReportSummary,
} from "@/lib/offline-report-queue";
import { useOnlineStatus } from "@/lib/pwa";
import { fieldClassName, PageHeader, Panel, ScoreBar } from "@/components/ui-kit";

const reportSteps = ["Describe", "Locate", "Review"] as const;

type IssueOption = {
  id: string;
  icon: LucideIcon;
  label: string;
  prompt: string;
  signal: string;
};

const issueOptions: IssueOption[] = [
  {
    id: "road",
    icon: Road,
    label: "Road damage",
    prompt: "Large pothole near Pine Street bus stop with cars swerving around it.",
    signal: "DOT route",
  },
  {
    id: "drainage",
    icon: Droplets,
    label: "Drainage",
    prompt: "Blocked storm drain with water pooling near the curb after heavy rain.",
    signal: "DPW route",
  },
  {
    id: "signal",
    icon: TrafficCone,
    label: "Traffic signal",
    prompt: "Traffic signal is dark at a busy intersection during evening traffic.",
    signal: "Priority review",
  },
  {
    id: "light",
    icon: Lightbulb,
    label: "Streetlight",
    prompt: "Streetlight is not working near a crosswalk and the area is very dark.",
    signal: "Utilities route",
  },
];

const contactOptions = ["Text message", "Email", "No updates"] as const;
const reportDraftStorageKey = "civic-signal-report-draft-v1";

type ContactPreference = (typeof contactOptions)[number];

type ReportDraft = {
  address: string;
  contactPreference: ContactPreference;
  description: string;
  issueType: string;
  latitude: string;
  longitude: string;
  updatedAt: string;
};

type SubmissionProgressStepStatus = "idle" | "running" | "complete" | "warning" | "error" | "skipped";

type SubmissionProgressStep = {
  detail: string;
  id: "create" | "upload" | "analyze" | "track";
  label: string;
  status: SubmissionProgressStepStatus;
};

type BackendTriageSnapshot = {
  duplicates: DuplicateCandidateDto[];
  prediction: TriagePredictionDto | null;
  status: IncidentProcessingStatusDto | null;
  timedOut: boolean;
};

const defaultReportDraft: ReportDraft = {
  address: "Pine St and 7th Ave",
  contactPreference: "Text message",
  description: issueOptions[0].prompt,
  issueType: issueOptions[0].id,
  latitude: "40.7128",
  longitude: "-74.0060",
  updatedAt: "",
};

const defaultSubmissionProgress: SubmissionProgressStep[] = [
  {
    detail: "Waiting for review.",
    id: "create",
    label: "Create Incident",
    status: "idle",
  },
  {
    detail: "Evidence is optional.",
    id: "upload",
    label: "Upload Evidence",
    status: "idle",
  },
  {
    detail: "Prediction and duplicate status appear when available.",
    id: "analyze",
    label: "Check Triage",
    status: "idle",
  },
  {
    detail: "Tracking link appears after submission.",
    id: "track",
    label: "Ready To Track",
    status: "idle",
  },
];

export function CitizenReportPortal() {
  const online = useOnlineStatus();
  const draftReady = useRef(false);
  const skipNextDraftSave = useRef(false);
  const cameraInputRef = useRef<HTMLInputElement | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [step, setStep] = useState<(typeof reportSteps)[number]>("Describe");
  const [issueType, setIssueType] = useState(defaultReportDraft.issueType);
  const [description, setDescription] = useState(defaultReportDraft.description);
  const [address, setAddress] = useState(defaultReportDraft.address);
  const [latitude, setLatitude] = useState(defaultReportDraft.latitude);
  const [longitude, setLongitude] = useState(defaultReportDraft.longitude);
  const [mediaName, setMediaName] = useState("");
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [mediaPreviewUrl, setMediaPreviewUrl] = useState<string | null>(null);
  const [contactPreference, setContactPreference] = useState<ContactPreference>(defaultReportDraft.contactPreference);
  const [submittedCode, setSubmittedCode] = useState<string | null>(null);
  const [submittedIncident, setSubmittedIncident] = useState<IncidentDto | null>(null);
  const [uploadedMedia, setUploadedMedia] = useState<IncidentMediaDto | null>(null);
  const [backendStatus, setBackendStatus] = useState<IncidentProcessingStatusDto | null>(null);
  const [backendPrediction, setBackendPrediction] = useState<TriagePredictionDto | null>(null);
  const [backendDuplicates, setBackendDuplicates] = useState<DuplicateCandidateDto[]>([]);
  const [submissionProgress, setSubmissionProgress] = useState<SubmissionProgressStep[]>(defaultSubmissionProgress);
  const [submissionState, setSubmissionState] = useState<"idle" | "submitting" | "success" | "warning" | "error">("idle");
  const [submissionMessage, setSubmissionMessage] = useState("Ready to submit");
  const [draftMessage, setDraftMessage] = useState("Draft recovery is ready on this device.");
  const [dragActive, setDragActive] = useState(false);
  const [locationState, setLocationState] = useState<"idle" | "locating" | "success" | "error">("idle");
  const [locationMessage, setLocationMessage] = useState("Manual location is available if browser permission is not granted.");
  const [locationSearchState, setLocationSearchState] = useState<"idle" | "searching" | "success" | "error">("idle");
  const [locationSearchMessage, setLocationSearchMessage] = useState("Search an address or intersection, then choose the closest match.");
  const [locationSearchResults, setLocationSearchResults] = useState<GeocodingResultDto[]>([]);
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const recordingChunksRef = useRef<Blob[]>([]);
  const recordingStartedAtRef = useRef(0);
  const recordingStreamRef = useRef<MediaStream | null>(null);
  const [recording, setRecording] = useState(false);
  const [recordingState, setRecordingState] = useState<"idle" | "requesting" | "recording" | "ready" | "error">("idle");
  const [recordingMessage, setRecordingMessage] = useState("Record a short voice note from this browser.");
  const [voiceSeconds, setVoiceSeconds] = useState(0);
  const [copyState, setCopyState] = useState("Copy");
  const [queuedReports, setQueuedReports] = useState<QueuedPublicReportSummary[]>([]);
  const [offlineQueueMessage, setOfflineQueueMessage] = useState("Offline queue is ready.");
  const [offlineQueueSyncing, setOfflineQueueSyncing] = useState(false);

  const selectedIssue = useMemo(
    () => issueOptions.find((option) => option.id === issueType) ?? issueOptions[0],
    [issueType],
  );
  const analysis = useMemo(
    () => analyzeDraft(description, latitude, longitude, Boolean(mediaName)),
    [description, latitude, longitude, mediaName],
  );
  const readiness = Math.round(analysis.readiness);
  const routingTitle = backendPrediction
    ? `${backendPrediction.category} - ${backendPrediction.suggestedAgencyCode} - ${backendPrediction.severity} urgency`
    : `${analysis.category} - ${analysis.agency} - ${analysis.severity} urgency`;
  const routingSummary = backendPrediction?.summary ?? analysis.summary;
  const routingConfidence = backendPrediction ? Math.round(backendPrediction.confidence * 100) : readiness - 6;
  const duplicateRows =
    backendDuplicates.length > 0
      ? backendDuplicates.map((candidate) => ({
          caseId: candidate.candidateIncidentId,
          detail: candidate.reason ?? "Backend duplicate candidate",
          score: candidate.similarityScore,
        }))
      : analysis.duplicates.map((candidate) => ({
          caseId: candidate.caseId,
          detail: `${candidate.distanceMeters}m from your report`,
          score: candidate.score,
        }));
  const submissionProgressScore = Math.round(
    (submissionProgress.filter((item) => ["complete", "skipped", "warning"].includes(item.status)).length / submissionProgress.length) * 100,
  );

  const refreshOfflineQueue = useCallback(async () => {
    try {
      const reports = await listQueuedPublicReports();
      setQueuedReports(reports);
      setOfflineQueueMessage(
        reports.length
          ? `${reports.length} report${reports.length === 1 ? "" : "s"} waiting to sync.`
          : "Offline queue is empty.",
      );
    } catch {
      setQueuedReports([]);
      setOfflineQueueMessage("Offline queue is unavailable in this browser.");
    }
  }, []);

  const syncOfflineQueue = useCallback(
    async ({ silent = false }: { silent?: boolean } = {}) => {
      if (!online) {
        setOfflineQueueMessage("Connect to the internet to sync queued reports.");
        return;
      }

      if (offlineQueueSyncing) {
        return;
      }

      setOfflineQueueSyncing(true);
      if (!silent) {
        setOfflineQueueMessage("Syncing queued reports...");
      }

      try {
        const result = await syncQueuedPublicReports({
          onProgress: (event) => {
            if (event.type === "syncing") {
              setOfflineQueueMessage(`Syncing ${formatQueuedReportId(event.queuedReport.id)}...`);
              return;
            }

            if (event.type === "synced") {
              setSubmittedIncident(event.incident);
              setSubmittedCode(event.incident.trackingCode);
              setUploadedMedia(event.media ?? null);
              setBackendStatus(null);
              setBackendPrediction(null);
              setBackendDuplicates([]);
              setSubmissionState("success");
              setSubmissionMessage(`Queued report synced as ${event.incident.trackingCode}.`);
              setSubmissionProgress((current) => updateSubmissionProgress(current, "track", "complete", "Tracking page is ready."));
              setStep("Review");
              setCopyState("Copy");
              setOfflineQueueMessage(`Synced ${event.incident.trackingCode}.`);
              return;
            }

            setOfflineQueueMessage(`Sync failed: ${event.error}`);
          },
        });

        await refreshOfflineQueue();

        if (result.attempted === 0) {
          setOfflineQueueMessage("Offline queue is empty.");
        } else if (result.failed > 0) {
          setOfflineQueueMessage(`${result.completed} synced, ${result.failed} still waiting.`);
        } else {
          setOfflineQueueMessage(`${result.completed} queued report${result.completed === 1 ? "" : "s"} synced.`);
        }
      } catch {
        setOfflineQueueMessage("Could not sync queued reports.");
      } finally {
        setOfflineQueueSyncing(false);
      }
    },
    [offlineQueueSyncing, online, refreshOfflineQueue],
  );

  useEffect(() => {
    const timer = window.setTimeout(() => {
      const storedDraft = readReportDraft();

      if (storedDraft) {
        setIssueType(storedDraft.issueType);
        setDescription(storedDraft.description);
        setAddress(storedDraft.address);
        setLatitude(storedDraft.latitude);
        setLongitude(storedDraft.longitude);
        setContactPreference(storedDraft.contactPreference);
        setDraftMessage(`Draft restored from ${formatDraftTime(storedDraft.updatedAt)}.`);
      }

      draftReady.current = true;
    }, 0);

    return () => window.clearTimeout(timer);
  }, []);

  useEffect(() => {
    let active = true;

    async function loadQueue() {
      if (!active) {
        return;
      }

      await refreshOfflineQueue();
    }

    void loadQueue();
    const unsubscribe = subscribeOfflineReportQueue(() => {
      void loadQueue();
    });

    return () => {
      active = false;
      unsubscribe();
    };
  }, [refreshOfflineQueue]);

  useEffect(() => {
    if (!online || queuedReports.length === 0 || offlineQueueSyncing) {
      return;
    }

    const timer = window.setTimeout(() => {
      void syncOfflineQueue({ silent: true });
    }, 800);

    return () => window.clearTimeout(timer);
  }, [offlineQueueSyncing, online, queuedReports.length, syncOfflineQueue]);

  useEffect(() => {
    if (!draftReady.current) {
      return;
    }

    if (skipNextDraftSave.current) {
      skipNextDraftSave.current = false;
      return;
    }

    const timer = window.setTimeout(() => {
      saveReportDraft({
        address,
        contactPreference,
        description,
        issueType,
        latitude,
        longitude,
        updatedAt: new Date().toISOString(),
      });
    }, 250);

    return () => window.clearTimeout(timer);
  }, [address, contactPreference, description, issueType, latitude, longitude]);

  useEffect(() => {
    if (!recording) {
      return;
    }

    const timer = window.setInterval(() => {
      setVoiceSeconds((current) => current + 1);
    }, 1000);

    return () => window.clearInterval(timer);
  }, [recording]);

  useEffect(() => {
    return () => {
      if (mediaPreviewUrl) {
        URL.revokeObjectURL(mediaPreviewUrl);
      }
    };
  }, [mediaPreviewUrl]);

  useEffect(() => {
    return () => {
      const recorder = mediaRecorderRef.current;
      if (recorder && recorder.state !== "inactive") {
        recorder.ondataavailable = null;
        recorder.onstop = null;
        recorder.stop();
      }

      recordingStreamRef.current?.getTracks().forEach((track) => track.stop());
    };
  }, []);

  function selectIssue(option: IssueOption) {
    setIssueType(option.id);
    setDescription(option.prompt);
    setSubmittedCode(null);
    setSubmittedIncident(null);
    setUploadedMedia(null);
    setBackendStatus(null);
    setBackendPrediction(null);
    setBackendDuplicates([]);
    setSubmissionProgress(cloneSubmissionProgress());
    setSubmissionState("idle");
    setSubmissionMessage("Ready to submit");
    setStep("Describe");
  }

  function clearDraft() {
    clearStoredReportDraft();
    draftReady.current = true;
    skipNextDraftSave.current = true;
    setIssueType(defaultReportDraft.issueType);
    setDescription(defaultReportDraft.description);
    setAddress(defaultReportDraft.address);
    setLatitude(defaultReportDraft.latitude);
    setLongitude(defaultReportDraft.longitude);
    setContactPreference(defaultReportDraft.contactPreference);
    setDraftMessage("Draft cleared. A fresh report is ready.");
    setSubmittedCode(null);
    setSubmittedIncident(null);
    setUploadedMedia(null);
    setBackendStatus(null);
    setBackendPrediction(null);
    setBackendDuplicates([]);
    setSubmissionProgress(cloneSubmissionProgress());
    setSubmissionState("idle");
    setSubmissionMessage("Ready to submit");
    clearMedia();
    setStep("Describe");
  }

  function attachFile(file: File) {
    setMediaName(file.name);
    setSelectedFile(file);
    cancelActiveRecording();
    setRecordingState(file.type.startsWith("audio/") ? "ready" : "idle");
    setRecordingMessage(
      file.type.startsWith("audio/")
        ? "Audio evidence is ready to upload."
        : `${formatEvidenceKind(file.type)} evidence is ready to upload.`,
    );

    if (mediaPreviewUrl) {
      URL.revokeObjectURL(mediaPreviewUrl);
    }

    setMediaPreviewUrl(canPreviewMedia(file.type) ? URL.createObjectURL(file) : null);
  }

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (file) {
      attachFile(file);
    }

    event.currentTarget.value = "";
  }

  function openCameraCapture() {
    cameraInputRef.current?.click();
  }

  function openFilePicker() {
    fileInputRef.current?.click();
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setDragActive(false);

    const file = event.dataTransfer.files?.[0];
    if (file) {
      attachFile(file);
    }
  }

  async function searchAddress() {
    const query = address.trim();

    if (query.length < 3) {
      setLocationSearchState("error");
      setLocationSearchMessage("Enter at least 3 characters to search for an address.");
      return;
    }

    if (!online) {
      setLocationSearchState("error");
      setLocationSearchMessage("Address search needs a network connection. Coordinates can still be entered manually.");
      return;
    }

    setLocationSearchState("searching");
    setLocationSearchMessage("Searching the backend geocoder...");
    setLocationSearchResults([]);

    try {
      const results = await searchLocations(query);
      setLocationSearchResults(results);
      setLocationSearchState(results.length > 0 ? "success" : "idle");
      setLocationSearchMessage(
        results.length > 0
          ? `Found ${results.length} location ${results.length === 1 ? "match" : "matches"}.`
          : "No address matches returned. Check the spelling or enter coordinates manually.",
      );
    } catch (error) {
      setLocationSearchState("error");
      setLocationSearchMessage(error instanceof CivicApiError ? error.message : "Address search failed.");
    }
  }

  function selectLocationResult(result: GeocodingResultDto) {
    setAddress(formatGeocodingLabel(result));
    setLatitude(result.latitude.toFixed(5));
    setLongitude(result.longitude.toFixed(5));
    setLocationState("success");
    setLocationMessage("Address match selected and coordinates filled.");
    setLocationSearchState("success");
    setLocationSearchMessage("Selected location is ready for review.");
  }

  function useCurrentLocation() {
    if (!navigator.geolocation) {
      setLocationState("error");
      setLocationMessage("This browser does not support location capture. Enter coordinates manually.");
      return;
    }

    setLocationState("locating");
    setLocationMessage("Waiting for browser location permission...");

    navigator.geolocation.getCurrentPosition(
      (position) => {
        const nextLatitude = position.coords.latitude.toFixed(5);
        const nextLongitude = position.coords.longitude.toFixed(5);
        const accuracy = Math.round(position.coords.accuracy);

        setAddress("Current device location");
        setLatitude(nextLatitude);
        setLongitude(nextLongitude);
        setLocationState("success");
        setLocationMessage(`Location captured with about ${accuracy}m accuracy.`);
        setStep("Review");
        void reverseGeocode(Number(nextLatitude), Number(nextLongitude))
          .then((result) => {
            setAddress(formatGeocodingLabel(result));
            setLocationMessage(`Location captured with about ${accuracy}m accuracy and matched to an address.`);
          })
          .catch(() => {
            setLocationMessage(`Location captured with about ${accuracy}m accuracy. Reverse address lookup is unavailable.`);
          });
      },
      (error) => {
        setLocationState("error");
        setLocationMessage(getLocationErrorMessage(error));
      },
      {
        enableHighAccuracy: true,
        maximumAge: 30000,
        timeout: 12000,
      },
    );
  }

  async function toggleRecording() {
    if (recording) {
      stopActiveRecording();
      return;
    }

    if (!navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === "undefined") {
      setRecordingState("error");
      setRecordingMessage("This browser does not support microphone recording. Upload an audio file instead.");
      return;
    }

    if (mediaPreviewUrl) {
      URL.revokeObjectURL(mediaPreviewUrl);
    }

    cancelActiveRecording();
    setMediaPreviewUrl(null);
    setSelectedFile(null);
    setMediaName("Requesting microphone...");
    setRecordingState("requesting");
    setRecordingMessage("Waiting for microphone permission...");

    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const mimeType = getPreferredAudioMimeType();
      const recorder = new MediaRecorder(stream, mimeType ? { mimeType } : undefined);

      recordingChunksRef.current = [];
      recordingStartedAtRef.current = Date.now();
      recordingStreamRef.current = stream;
      mediaRecorderRef.current = recorder;

      recorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          recordingChunksRef.current.push(event.data);
        }
      };

      recorder.onstop = () => {
        const recordingType = recorder.mimeType || mimeType || "audio/webm";
        const audioBlob = new Blob(recordingChunksRef.current, { type: recordingType });
        const durationSeconds = Math.max(1, Math.round((Date.now() - recordingStartedAtRef.current) / 1000));

        stopRecordingStream();
        mediaRecorderRef.current = null;
        recordingChunksRef.current = [];
        setRecording(false);

        if (audioBlob.size === 0) {
          setSelectedFile(null);
          setMediaName("");
          setRecordingState("error");
          setRecordingMessage("No audio was captured. Try recording again or upload an audio file.");
          return;
        }

        const audioFile = new File([audioBlob], `voice-note-${Date.now()}.${audioFileExtension(recordingType)}`, {
          lastModified: Date.now(),
          type: recordingType,
        });
        const previewUrl = URL.createObjectURL(audioBlob);

        setSelectedFile(audioFile);
        setMediaName(audioFile.name);
        setMediaPreviewUrl(previewUrl);
        setRecordingState("ready");
        setRecordingMessage(`Voice note captured: ${formatSeconds(durationSeconds)} and ${formatFileSize(audioFile.size)}.`);
      };

      recorder.start(1000);
      setRecording(true);
      setVoiceSeconds(0);
      setMediaName("Recording voice note...");
      setRecordingState("recording");
      setRecordingMessage("Recording from your microphone. Stop when the report is clear.");
    } catch (error) {
      cancelActiveRecording();
      setRecordingState("error");
      setRecordingMessage(getMicrophoneErrorMessage(error));
      setMediaName("");
    }
  }

  function stopActiveRecording() {
    const recorder = mediaRecorderRef.current;

    if (!recorder || recorder.state === "inactive") {
      setRecording(false);
      setRecordingState("idle");
      setRecordingMessage("Record a short voice note from this browser.");
      return;
    }

    setRecording(false);
    setRecordingState("requesting");
    setRecordingMessage("Finalizing voice note...");
    setMediaName("Finalizing voice note...");

    try {
      recorder.requestData();
    } catch {
      // Some browsers throw if requestData runs while a stop is already pending.
    }

    recorder.stop();
  }

  function cancelActiveRecording() {
    const recorder = mediaRecorderRef.current;

    if (recorder) {
      recorder.ondataavailable = null;
      recorder.onstop = null;

      if (recorder.state !== "inactive") {
        recorder.stop();
      }
    }

    stopRecordingStream();
    mediaRecorderRef.current = null;
    recordingChunksRef.current = [];
    recordingStartedAtRef.current = 0;
    setRecording(false);
    setVoiceSeconds(0);
  }

  function stopRecordingStream() {
    recordingStreamRef.current?.getTracks().forEach((track) => track.stop());
    recordingStreamRef.current = null;
  }

  function clearMedia() {
    if (mediaPreviewUrl) {
      URL.revokeObjectURL(mediaPreviewUrl);
    }

    cancelActiveRecording();
    setMediaPreviewUrl(null);
    setSelectedFile(null);
    setMediaName("");
    setRecording(false);
    setVoiceSeconds(0);
    setRecordingState("idle");
    setRecordingMessage("Record a short voice note from this browser.");
  }

  async function queueCurrentReport(parsedLatitude: number, parsedLongitude: number, reason: "offline" | "network") {
    setSubmissionState("submitting");
    setSubmissionMessage("Saving report to the offline queue...");
    setSubmissionProgress(
      updateSubmissionProgress(
        cloneSubmissionProgress(),
        "create",
        "running",
        "Saving description, coordinates, and media on this device.",
      ),
    );

    try {
      const queued = await enqueuePublicReport({
        address,
        contactPreference,
        description: buildIncidentDescription(selectedIssue.label, address, description),
        issueType,
        latitude: parsedLatitude,
        longitude: parsedLongitude,
        mediaFile: selectedFile,
      });

      setSubmissionProgress((current) =>
        updateSubmissionProgress(
          current,
          "create",
          "warning",
          reason === "offline" ? "Queued until this device is online." : "Network dropped; queued for retry.",
        ),
      );
      setSubmissionProgress((current) =>
        updateSubmissionProgress(
          current,
          "upload",
          selectedFile ? "warning" : "skipped",
          selectedFile ? `${selectedFile.name} is stored with the queued report.` : "No evidence file selected.",
        ),
      );
      setSubmissionProgress((current) =>
        updateSubmissionProgress(current, "analyze", "idle", "AI triage starts after the queued report syncs."),
      );
      setSubmissionProgress((current) =>
        updateSubmissionProgress(current, "track", "warning", "Tracking code appears after sync."),
      );
      setSubmissionState("warning");
      setSubmissionMessage(`Report saved offline as ${formatQueuedReportId(queued.id)}. It will sync automatically when online.`);
      setDraftMessage("Draft is safely stored in the offline queue.");
      setStep("Review");
      clearStoredReportDraft();
      await refreshOfflineQueue();
    } catch (error) {
      setSubmissionProgress((current) => failRunningSubmissionProgress(current, getOfflineQueueErrorMessage(error)));
      setSubmissionState("error");
      setSubmissionMessage(getOfflineQueueErrorMessage(error));
    }
  }

  async function submitReport() {
    const parsedLatitude = Number.parseFloat(latitude);
    const parsedLongitude = Number.parseFloat(longitude);

    if (recording) {
      setSubmissionState("error");
      setSubmissionMessage("Stop the voice recording before submitting the report.");
      return;
    }

    if (description.trim().length < 10) {
      setSubmissionState("error");
      setSubmissionMessage("Please add a little more detail before submitting.");
      return;
    }

    if (!Number.isFinite(parsedLatitude) || !Number.isFinite(parsedLongitude)) {
      setSubmissionState("error");
      setSubmissionMessage("Latitude and longitude must be valid numbers.");
      return;
    }

    if (!online) {
      await queueCurrentReport(parsedLatitude, parsedLongitude, "offline");
      return;
    }

    setSubmissionState("submitting");
    setSubmissionMessage("Creating incident...");
    setSubmissionProgress(updateSubmissionProgress(cloneSubmissionProgress(), "create", "running", "Sending description and coordinates to the API."));
    setSubmittedCode(null);
    setSubmittedIncident(null);
    setUploadedMedia(null);
    setBackendStatus(null);
    setBackendPrediction(null);
    setBackendDuplicates([]);

    try {
      const incident = await createIncident({
        description: buildIncidentDescription(selectedIssue.label, address, description),
        latitude: parsedLatitude,
        longitude: parsedLongitude,
      });

      const trackingCode = incident.trackingCode;

      setSubmissionProgress((current) => updateSubmissionProgress(current, "create", "complete", `Incident ${trackingCode} created.`));
      setSubmissionMessage(selectedFile ? "Uploading evidence..." : "Checking triage status...");

      let media: IncidentMediaDto | null = null;
      let mediaWarning: string | null = null;

      if (selectedFile) {
        setSubmissionProgress((current) =>
          updateSubmissionProgress(current, "upload", "running", `${selectedFile.name} is being uploaded.`),
        );

        try {
          const uploaded = await uploadPublicIncidentMedia(trackingCode, selectedFile);
          media = uploaded;
          setSubmissionProgress((current) =>
            updateSubmissionProgress(current, "upload", "complete", `${uploaded.fileName} attached to the incident.`),
          );
        } catch (error) {
          mediaWarning = error instanceof CivicApiError ? error.message : "Media upload failed.";
          setSubmissionProgress((current) => updateSubmissionProgress(current, "upload", "warning", mediaWarning ?? "Media upload failed."));
        }
      } else {
        setSubmissionProgress((current) => updateSubmissionProgress(current, "upload", "skipped", "No evidence file selected."));
      }

      setSubmissionMessage("Checking prediction and duplicate status...");
      setSubmissionProgress((current) => updateSubmissionProgress(current, "analyze", "running", "Loading status, prediction, and duplicate candidates."));

      const triageSnapshot = await waitForBackendTriage(trackingCode, (attempt) => {
        setSubmissionProgress((current) =>
          updateSubmissionProgress(
            current,
            "analyze",
            "running",
            `Waiting for worker AI result, attempt ${attempt}.`,
          ),
        );
      });

      setSubmissionProgress((current) =>
        updateSubmissionProgress(
          current,
          "analyze",
          triageSnapshot.timedOut ? "warning" : "complete",
          triageSnapshot.prediction
            ? `${triageSnapshot.prediction.category} prediction loaded from worker AI.`
            : triageSnapshot.timedOut
              ? "AI processing is still running; use the status page for updates."
              : "Backend processing completed without a prediction.",
        ),
      );
      setSubmissionProgress((current) => updateSubmissionProgress(current, "track", "complete", "Tracking page is ready."));
      setSubmittedIncident(incident);
      setSubmittedCode(trackingCode);
      setUploadedMedia(media);
      setBackendStatus(triageSnapshot.status);
      setBackendPrediction(triageSnapshot.prediction);
      setBackendDuplicates(triageSnapshot.duplicates);
      savePublicReport(
        createStoredPublicReport(incident, {
          media,
          status: triageSnapshot.status?.incidentStatus,
        }),
      );
      setSubmissionState(mediaWarning || triageSnapshot.timedOut ? "warning" : "success");
      setSubmissionMessage(
        mediaWarning
          ? `Report submitted, but media was not attached: ${mediaWarning}`
          : triageSnapshot.timedOut
            ? "Report submitted. AI processing is still running in the background."
            : media
            ? "Report submitted with media evidence."
            : "Report submitted to the backend.",
      );
      setStep("Review");
      setCopyState("Copy");
      clearStoredReportDraft();
      setDraftMessage("Draft cleared after successful submission.");
    } catch (error) {
      if (shouldQueueAfterSubmissionFailure(error)) {
        await queueCurrentReport(parsedLatitude, parsedLongitude, "network");
        return;
      }

      const message = error instanceof CivicApiError ? error.message : "Could not reach the CivicSignal API.";
      setSubmissionProgress((current) => failRunningSubmissionProgress(current, message));
      setSubmissionState("error");
      setSubmissionMessage(message);
    }
  }

  async function copyReportCode() {
    if (!submittedCode) {
      return;
    }

    try {
      await navigator.clipboard.writeText(submittedCode);
      setCopyState("Copied");
    } catch {
      setCopyState("Manual copy");
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        actions={
          <div className="inline-flex items-center gap-2 rounded-md bg-civic-soft px-3 py-2 text-sm font-semibold text-civic-primary">
            <ShieldCheck className="h-4 w-4" aria-hidden="true" />
            Secure city intake
          </div>
        }
        description="A guided mobile-first workflow for reporting city issues with media, location, review, and status tracking."
        eyebrow="Resident Portal"
        title="Report A City Issue"
      />

      <Panel title="Choose Issue Type" description={`${selectedIssue.label} selected - ${selectedIssue.signal}`}>
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {issueOptions.map((option) => (
            <button
              aria-pressed={issueType === option.id}
              className={`group rounded-md border p-4 text-left transition hover:-translate-y-0.5 ${
                issueType === option.id
                  ? "border-civic-primary bg-civic-soft shadow-sm"
                  : "border-civic-border bg-civic-raised hover:border-civic-border-strong"
              }`}
              key={option.id}
              onClick={() => selectIssue(option)}
              type="button"
            >
              <span className="inline-flex rounded-md bg-civic-surface p-2 text-civic-primary transition group-hover:scale-105">
                <option.icon className="h-5 w-5" aria-hidden="true" />
              </span>
              <span className="mt-4 block text-base font-semibold text-civic-heading">{option.label}</span>
              <span className="mt-1 block text-sm text-civic-muted">{option.signal}</span>
            </button>
          ))}
        </div>
      </Panel>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,0.95fr)_minmax(360px,0.75fr)]">
        <Panel title="Guided Report" description={stepDescription(step)}>
          <div className="mb-4 flex flex-col gap-3 rounded-md border border-civic-border bg-civic-raised p-3 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div className="text-sm font-semibold text-civic-heading">Saved Draft</div>
              <p className="mt-1 text-sm text-civic-muted">{draftMessage}</p>
              {!online ? (
                <p className="mt-1 text-sm font-semibold text-status-critical-text">
                  Offline mode is active. Continue editing; submit will unlock after reconnecting.
                </p>
              ) : null}
            </div>
            <button
              className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface px-3 text-sm font-semibold text-civic-primary hover:bg-white"
              onClick={clearDraft}
              type="button"
            >
              <RotateCcw className="h-4 w-4" aria-hidden="true" />
              Reset
            </button>
          </div>

          <div className="mb-6 grid gap-2 sm:grid-cols-3">
            {reportSteps.map((item, index) => (
              <button
                aria-pressed={step === item}
                className={`rounded-md border p-4 text-left transition hover:-translate-y-0.5 ${
                  step === item ? "border-civic-primary bg-civic-soft" : "border-civic-border bg-civic-raised hover:border-civic-border-strong"
                }`}
                key={item}
                onClick={() => setStep(item)}
                type="button"
              >
                <span className="flex h-7 w-7 items-center justify-center rounded-md bg-civic-surface text-sm font-semibold text-civic-primary">
                  {index + 1}
                </span>
                <span className="mt-3 block text-sm font-semibold text-civic-heading">{item}</span>
              </button>
            ))}
          </div>

          {step === "Describe" ? (
            <div className="grid gap-5">
              <label className="block">
                <span className="text-sm font-semibold text-civic-heading">What happened?</span>
                <textarea
                  className={`${fieldClassName} mt-2 min-h-40 resize-none`}
                  onChange={(event) => setDescription(event.target.value)}
                  value={description}
                />
              </label>

              <div
                className={`rounded-lg border border-dashed p-5 transition ${
                  dragActive ? "border-civic-primary bg-civic-soft" : "border-civic-border-strong bg-civic-raised hover:bg-civic-soft"
                }`}
                onDragLeave={() => setDragActive(false)}
                onDragOver={(event) => {
                  event.preventDefault();
                  setDragActive(true);
                }}
                onDrop={handleDrop}
              >
                <input
                  accept="image/*"
                  capture="environment"
                  className="sr-only"
                  onChange={handleFileChange}
                  ref={cameraInputRef}
                  type="file"
                />
                <input
                  accept="image/*,audio/*,video/*,application/pdf"
                  className="sr-only"
                  onChange={handleFileChange}
                  ref={fileInputRef}
                  type="file"
                />
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex items-center gap-3">
                    <span className="rounded-md bg-civic-surface p-3 text-civic-primary">
                      <Upload className="h-5 w-5" aria-hidden="true" />
                    </span>
                    <span>
                      <span className="block text-sm font-semibold text-civic-heading">Attach evidence</span>
                      <span className="mt-1 block text-sm text-civic-muted">
                        {selectedFile ? `${formatEvidenceKind(selectedFile.type)} - ${formatFileSize(selectedFile.size)}` : "Drop media here or choose a capture option"}
                      </span>
                    </span>
                  </div>
                  <div className="grid gap-2 sm:grid-cols-2">
                    <button
                      className="inline-flex h-11 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-sm font-semibold text-white hover:bg-civic-primary-strong"
                      onClick={openCameraCapture}
                      type="button"
                    >
                      <Camera className="h-4 w-4" aria-hidden="true" />
                      Take Photo
                    </button>
                    <button
                      className="inline-flex h-11 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface px-4 text-sm font-semibold text-civic-primary hover:bg-white"
                      onClick={openFilePicker}
                      type="button"
                    >
                    <Upload className="h-5 w-5" aria-hidden="true" />
                      Upload File
                    </button>
                  </div>
                </div>
              </div>

              <div className="rounded-lg border border-civic-border bg-civic-raised p-4">
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
                      <Mic className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                      Voice note
                    </div>
                    <p className="mt-1 text-sm text-civic-muted">{recording ? `Recording ${formatSeconds(voiceSeconds)}` : mediaName || "No audio selected"}</p>
                    <p
                      className={`mt-1 text-sm font-semibold ${
                        recordingState === "error"
                          ? "text-status-critical-text"
                          : recordingState === "ready"
                            ? "text-status-approved-text"
                            : "text-civic-muted"
                      }`}
                    >
                      {recordingMessage}
                    </p>
                  </div>
                  <button
                    className={`inline-flex h-11 items-center justify-center gap-2 rounded-md px-4 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-60 ${
                      recording
                        ? "bg-status-critical text-status-critical-text hover:bg-status-critical/80"
                        : "bg-civic-primary text-white hover:bg-civic-primary-strong"
                    }`}
                    disabled={recordingState === "requesting"}
                    onClick={toggleRecording}
                    type="button"
                  >
                    {recording ? <Pause className="h-4 w-4" aria-hidden="true" /> : <Play className="h-4 w-4" aria-hidden="true" />}
                    {recording ? "Stop" : recordingState === "requesting" ? "Opening Mic..." : recordingState === "ready" ? "Record Again" : "Record"}
                  </button>
                </div>
                <div className="mt-4 flex h-12 items-end gap-1">
                  {Array.from({ length: 28 }, (_, index) => (
                    <span
                      className={`w-full rounded-t bg-civic-primary/70 ${recording ? "wave-bar" : ""}`}
                      key={index}
                      style={{
                        animationDelay: `${index * 45}ms`,
                        height: `${12 + ((index * 17) % 30)}px`,
                      }}
                    />
                  ))}
                </div>
              </div>

              <button
                className="inline-flex h-12 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-base font-semibold text-white hover:bg-civic-primary-strong"
                onClick={() => setStep("Locate")}
                type="button"
              >
                <Navigation className="h-5 w-5" aria-hidden="true" />
                Continue To Location
              </button>
            </div>
          ) : null}

          {step === "Locate" ? (
            <div className="grid gap-5">
              <div className="rounded-lg border border-civic-border bg-civic-raised p-4">
                <label className="block">
                  <span className="text-sm font-semibold text-civic-heading">Address or intersection</span>
                  <div className="mt-2 grid gap-2 sm:grid-cols-[1fr_auto]">
                    <input
                      className={fieldClassName}
                      onChange={(event) => setAddress(event.target.value)}
                      onKeyDown={(event) => {
                        if (event.key === "Enter") {
                          event.preventDefault();
                          void searchAddress();
                        }
                      }}
                      value={address}
                    />
                    <button
                      className="inline-flex h-12 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-base font-semibold text-white transition hover:bg-civic-primary-strong disabled:cursor-not-allowed disabled:opacity-60"
                      disabled={locationSearchState === "searching"}
                      onClick={() => void searchAddress()}
                      type="button"
                    >
                      <Search className="h-5 w-5" aria-hidden="true" />
                      {locationSearchState === "searching" ? "Searching..." : "Search"}
                    </button>
                  </div>
                </label>

                <div
                  className={`mt-3 rounded-md border p-3 text-sm font-semibold ${
                    locationSearchState === "error"
                      ? "border-status-critical bg-status-critical/10 text-status-critical-text"
                      : locationSearchState === "success"
                        ? "border-status-approved bg-status-approved/10 text-status-approved-text"
                        : "border-civic-border bg-civic-surface text-civic-muted"
                  }`}
                >
                  {locationSearchMessage}
                </div>

                {locationSearchResults.length > 0 ? (
                  <div className="mt-3 grid gap-2">
                    {locationSearchResults.slice(0, 4).map((result) => (
                      <button
                        className="rounded-md border border-civic-border bg-civic-surface p-3 text-left transition hover:border-civic-primary hover:bg-white"
                        key={`${result.latitude}-${result.longitude}-${result.displayName}`}
                        onClick={() => selectLocationResult(result)}
                        type="button"
                      >
                        <span className="block text-sm font-semibold text-civic-heading">{formatGeocodingLabel(result)}</span>
                        <span className="mt-1 block text-sm text-civic-muted">{formatGeocodingSecondary(result)}</span>
                      </button>
                    ))}
                  </div>
                ) : null}
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <label className="block">
                  <span className="text-sm font-semibold text-civic-heading">Latitude</span>
                  <input
                    className={`${fieldClassName} mt-2`}
                    inputMode="decimal"
                    onChange={(event) => setLatitude(event.target.value)}
                    value={latitude}
                  />
                </label>
                <label className="block">
                  <span className="text-sm font-semibold text-civic-heading">Longitude</span>
                  <input
                    className={`${fieldClassName} mt-2`}
                    inputMode="decimal"
                    onChange={(event) => setLongitude(event.target.value)}
                    value={longitude}
                  />
                </label>
              </div>

              <div className="relative min-h-64 overflow-hidden rounded-lg border border-civic-border bg-civic-raised p-4">
                <div className="absolute inset-0 grid grid-cols-6 grid-rows-6 opacity-70">
                  {Array.from({ length: 36 }, (_, index) => (
                    <span className="border-b border-r border-civic-border" key={index} />
                  ))}
                </div>
                <div className="absolute left-[47%] top-[44%]">
                  <span className="absolute -left-5 -top-5 h-10 w-10 rounded-full bg-civic-primary/20 status-dot" />
                  <span className="relative flex h-10 w-10 items-center justify-center rounded-full bg-civic-primary text-white shadow-lg">
                    <MapPin className="h-5 w-5" aria-hidden="true" />
                  </span>
                </div>
                <div className="relative z-10 max-w-xs rounded-md border border-civic-border bg-civic-surface p-4 shadow-sm">
                  <div className="text-sm font-semibold text-civic-heading">{address}</div>
                  <p className="mt-1 text-sm text-civic-muted">
                    {latitude}, {longitude}
                  </p>
                </div>
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                <button
                  className="inline-flex h-12 items-center justify-center gap-2 rounded-md border border-civic-border px-4 text-base font-semibold text-civic-primary transition hover:bg-civic-soft disabled:cursor-not-allowed disabled:opacity-60"
                  disabled={locationState === "locating"}
                  onClick={useCurrentLocation}
                  type="button"
                >
                  <LocateFixed className="h-5 w-5" aria-hidden="true" />
                  {locationState === "locating" ? "Locating..." : "Use My Location"}
                </button>
                <button
                  className="inline-flex h-12 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-base font-semibold text-white hover:bg-civic-primary-strong"
                  onClick={() => setStep("Review")}
                  type="button"
                >
                  <CheckCircle2 className="h-5 w-5" aria-hidden="true" />
                  Review Report
                </button>
              </div>
              <div
                className={`rounded-md border p-3 text-sm font-semibold ${
                  locationState === "error"
                    ? "border-status-critical bg-status-critical/10 text-status-critical-text"
                    : locationState === "success"
                      ? "border-status-approved bg-status-approved/10 text-status-approved-text"
                      : "border-civic-border bg-civic-raised text-civic-muted"
                }`}
              >
                {locationMessage}
              </div>
            </div>
          ) : null}

          {step === "Review" ? (
            <div className="grid gap-5">
              <div className="rounded-md border border-civic-border bg-civic-raised p-4">
                <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
                  <MapPin className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                  {address}
                </div>
                <p className="mt-2 text-sm leading-6 text-civic-muted">{description}</p>
              </div>

              <div>
                <span className="text-sm font-semibold text-civic-heading">Status updates</span>
                <div className="mt-2 grid gap-2 sm:grid-cols-3">
                  {contactOptions.map((option) => (
                    <button
                      aria-pressed={contactPreference === option}
                      className={`h-11 rounded-md border px-3 text-sm font-semibold transition ${
                        contactPreference === option
                          ? "border-civic-primary bg-civic-primary text-white"
                          : "border-civic-border bg-civic-raised text-civic-muted hover:bg-civic-soft hover:text-civic-primary"
                      }`}
                      key={option}
                      onClick={() => setContactPreference(option)}
                      type="button"
                    >
                      {option}
                    </button>
                  ))}
                </div>
              </div>

              <button
                className="inline-flex h-12 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-base font-semibold text-white transition hover:bg-civic-primary-strong disabled:cursor-not-allowed disabled:opacity-60"
                disabled={submissionState === "submitting"}
                onClick={submitReport}
                type="button"
              >
                {online ? <CloudUpload className="h-5 w-5" aria-hidden="true" /> : <WifiOff className="h-5 w-5" aria-hidden="true" />}
                {submissionState === "submitting" ? "Submitting..." : online ? "Submit City Report" : "Queue Offline Report"}
              </button>
              <div
                className={`rounded-md border p-3 text-sm font-semibold ${
                  submissionState === "error"
                    ? "border-status-critical bg-status-critical/10 text-status-critical-text"
                    : submissionState === "warning"
                      ? "border-status-review bg-status-review/10 text-status-review-text"
                    : submissionState === "success"
                      ? "border-status-approved bg-status-approved/10 text-status-approved-text"
                      : "border-civic-border bg-civic-raised text-civic-muted"
                }`}
              >
                {submissionMessage}
              </div>
              <div className="rounded-md border border-civic-border bg-civic-raised p-4">
                <ScoreBar label="Submission progress" score={submissionProgressScore} />
                <div className="mt-4 grid gap-2">
                  {submissionProgress.map((item) => (
                    <div className="flex items-start gap-3 rounded-md border border-civic-border bg-civic-surface p-3" key={item.id}>
                      <span className={`mt-0.5 h-3 w-3 shrink-0 rounded-full ${submissionStepDotClass(item.status)}`} />
                      <span className="min-w-0">
                        <span className="block text-sm font-semibold text-civic-heading">{item.label}</span>
                        <span className="mt-1 block text-sm text-civic-muted">{item.detail}</span>
                      </span>
                    </div>
                  ))}
                </div>
              </div>

              <div className="rounded-md border border-civic-border bg-civic-raised p-4">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
                      <WifiOff className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                      Offline Queue
                    </div>
                    <p className="mt-1 text-sm leading-6 text-civic-muted">{offlineQueueMessage}</p>
                  </div>
                  <button
                    className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface px-3 text-sm font-semibold text-civic-primary transition hover:bg-white disabled:cursor-not-allowed disabled:opacity-60"
                    disabled={!online || offlineQueueSyncing || queuedReports.length === 0}
                    onClick={() => void syncOfflineQueue()}
                    type="button"
                  >
                    <RefreshCw className={`h-4 w-4 ${offlineQueueSyncing ? "animate-spin" : ""}`} aria-hidden="true" />
                    Sync Now
                  </button>
                </div>
                {queuedReports.length ? (
                  <div className="mt-4 grid gap-2">
                    {queuedReports.slice(0, 3).map((report) => (
                      <div className="rounded-md border border-civic-border bg-civic-surface p-3" key={report.id}>
                        <div className="flex items-center justify-between gap-3 text-sm">
                          <span className="font-semibold text-civic-heading">{formatQueuedReportId(report.id)}</span>
                          <span className="rounded-md bg-civic-soft px-2 py-1 text-xs font-semibold text-civic-primary">{report.status}</span>
                        </div>
                        <p className="mt-1 line-clamp-2 text-sm leading-6 text-civic-muted">{report.description}</p>
                        <div className="mt-2 flex flex-wrap gap-2 text-xs font-semibold text-civic-muted">
                          <span>{formatQueuedReportTime(report.createdAt)}</span>
                          {report.hasMedia ? <span>{report.mediaFileName ?? "Media attached"}</span> : <span>No media</span>}
                          {report.lastError ? <span className="text-status-critical-text">{report.lastError}</span> : null}
                        </div>
                      </div>
                    ))}
                  </div>
                ) : null}
              </div>
            </div>
          ) : null}
        </Panel>

        <div className="grid gap-6">
          <Panel title="Smart Routing Preview" description={routingTitle}>
            <div className="grid gap-4">
              <div className="rounded-md border border-civic-border bg-civic-raised p-4">
                <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
                  <Sparkles className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                  {routingSummary}
                </div>
              </div>
              <ScoreBar label="Report completeness" score={readiness} />
              <ScoreBar label="Routing confidence" score={routingConfidence} />
              <div className="grid gap-2">
                {duplicateRows.slice(0, 2).map((candidate) => (
                  <div className="rounded-md border border-civic-border bg-civic-raised p-3" key={candidate.caseId}>
                    <div className="flex items-center justify-between text-sm">
                      <span className="break-all font-semibold text-civic-heading">{formatReportCode(candidate.caseId)}</span>
                      <span className="font-semibold text-civic-primary">{Math.round(candidate.score * 100)}%</span>
                    </div>
                    <p className="mt-1 text-sm text-civic-muted">{candidate.detail}</p>
                  </div>
                ))}
              </div>
            </div>
          </Panel>

          <Panel title="Evidence Preview" description={uploadedMedia?.fileName ?? mediaName ?? "No media attached yet"}>
            <div className="flex h-44 items-center justify-center overflow-hidden rounded-md border border-civic-border bg-civic-raised">
              {mediaPreviewUrl && selectedFile?.type.startsWith("image/") ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img alt="Selected report evidence" className="h-full w-full object-cover" src={mediaPreviewUrl} />
              ) : mediaPreviewUrl && selectedFile?.type.startsWith("audio/") ? (
                <div className="w-full p-5">
                  <div className="mb-4 flex items-center gap-3 text-sm font-semibold text-civic-heading">
                    <span className="rounded-md bg-civic-surface p-2 text-civic-primary">
                      <Mic className="h-5 w-5" aria-hidden="true" />
                    </span>
                    {selectedFile.name}
                  </div>
                  <audio className="w-full" controls src={mediaPreviewUrl}>
                    Audio preview is unavailable in this browser.
                  </audio>
                </div>
              ) : mediaPreviewUrl && selectedFile?.type.startsWith("video/") ? (
                <video className="h-full w-full object-cover" controls src={mediaPreviewUrl}>
                  Video preview is unavailable in this browser.
                </video>
              ) : selectedFile ? (
                <div className="grid justify-items-center gap-3 p-5 text-center text-sm text-civic-muted">
                  <ClipboardList className="h-8 w-8 text-civic-primary" aria-hidden="true" />
                  <span className="font-semibold text-civic-heading">{selectedFile.name}</span>
                  <span>
                    {selectedFile.type || "Unknown file type"} - {formatFileSize(selectedFile.size)}
                  </span>
                </div>
              ) : (
                <div className="grid justify-items-center gap-3 text-center text-sm text-civic-muted">
                  <Camera className="h-8 w-8 text-civic-primary" aria-hidden="true" />
                  {mediaName || "Attach photo, video, or audio"}
                </div>
              )}
            </div>
            {selectedFile ? (
              <div className="mt-3 grid gap-2 rounded-md border border-civic-border bg-civic-raised p-3 text-sm text-civic-muted">
                <div className="flex items-center justify-between gap-3">
                  <span className="font-semibold text-civic-heading">Evidence type</span>
                  <span>{formatEvidenceKind(selectedFile.type)}</span>
                </div>
                <div className="flex items-center justify-between gap-3">
                  <span className="font-semibold text-civic-heading">File size</span>
                  <span>{formatFileSize(selectedFile.size)}</span>
                </div>
              </div>
            ) : null}
            {mediaName ? (
              <button
                className="mt-3 inline-flex h-10 items-center gap-2 rounded-md border border-civic-border px-3 text-sm font-semibold text-civic-primary hover:bg-civic-soft"
                onClick={clearMedia}
                type="button"
              >
                <Trash2 className="h-4 w-4" aria-hidden="true" />
                Remove
              </button>
            ) : null}
          </Panel>

          {submittedCode ? (
            <>
              <Panel title="Report Submitted" description="Use this code on the status page to follow updates.">
                <div className="rounded-md border border-civic-border bg-civic-soft p-4">
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                      <div className="break-all text-2xl font-semibold text-civic-heading">{submittedCode}</div>
                      <p className="mt-2 text-sm text-civic-muted">Updates will be sent by {contactPreference.toLowerCase()}.</p>
                      {submittedIncident ? (
                        <p className="mt-1 text-sm text-civic-muted">
                          Current backend status: <span className="font-semibold text-civic-primary">{backendStatus?.incidentStatus ?? submittedIncident.status}</span>
                        </p>
                      ) : null}
                    </div>
                    <button
                      className="inline-flex h-10 items-center justify-center gap-2 rounded-md bg-civic-primary px-3 text-sm font-semibold text-white hover:bg-civic-primary-strong"
                      onClick={copyReportCode}
                      type="button"
                    >
                      <Copy className="h-4 w-4" aria-hidden="true" />
                      {copyState}
                    </button>
                  </div>
                  <Link
                    className="mt-4 inline-flex h-11 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface px-3 text-sm font-semibold text-civic-primary hover:bg-white"
                    href={`/public/status?code=${encodeURIComponent(submittedCode)}`}
                  >
                    <ClipboardList className="h-4 w-4" aria-hidden="true" />
                    Track Status
                  </Link>
                  {backendStatus?.steps.length ? (
                    <div className="mt-4 grid gap-2">
                      {backendStatus.steps.slice(0, 4).map((item) => (
                        <div className="flex items-center justify-between rounded-md border border-civic-border bg-civic-surface px-3 py-2 text-sm" key={item.id}>
                          <span className="font-semibold text-civic-heading">{item.name}</span>
                          <span className="text-civic-muted">{item.status}</span>
                        </div>
                      ))}
                    </div>
                  ) : null}
                </div>
              </Panel>

              <SubmittedAiResult
                duplicates={backendDuplicates}
                prediction={backendPrediction}
                status={backendStatus}
                submissionState={submissionState}
                uploadedMedia={uploadedMedia}
              />
            </>
          ) : (
            <Panel title="What Happens Next" description="Your report moves through a transparent city workflow.">
              <div className="grid gap-3">
                {["Submitted", "AI assisted routing", "Staff review", "Agency response"].map((item) => (
                  <div className="flex items-center gap-3 rounded-md border border-civic-border bg-civic-raised p-3" key={item}>
                    <ClipboardList className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                    <span className="text-sm font-semibold text-civic-heading">{item}</span>
                  </div>
                ))}
              </div>
            </Panel>
          )}
        </div>
      </div>
    </div>
  );
}

function SubmittedAiResult({
  duplicates,
  prediction,
  status,
  submissionState,
  uploadedMedia,
}: {
  duplicates: DuplicateCandidateDto[];
  prediction: TriagePredictionDto | null;
  status: IncidentProcessingStatusDto | null;
  submissionState: "idle" | "submitting" | "success" | "warning" | "error";
  uploadedMedia: IncidentMediaDto | null;
}) {
  const confidence = prediction ? Math.round(prediction.confidence * 100) : 0;
  const modelLabel = prediction
    ? `${prediction.modelName}${prediction.modelVersion ? ` ${prediction.modelVersion}` : ""}`
    : "Worker AI pending";

  return (
    <Panel
      title="AI Result"
      description={prediction ? `Generated by ${modelLabel}` : "The backend worker is still preparing the AI triage result."}
    >
      {prediction ? (
        <div className="grid gap-4">
          <div className="rounded-md border border-civic-border bg-civic-raised p-4">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <div className="inline-flex items-center gap-2 rounded-md bg-civic-soft px-2 py-1 text-xs font-semibold text-civic-primary">
                  <Sparkles className="h-4 w-4" aria-hidden="true" />
                  AI-assisted triage
                </div>
                <h3 className="mt-3 text-xl font-semibold text-civic-heading">{prediction.category}</h3>
                <p className="mt-2 text-sm leading-6 text-civic-muted">{prediction.summary}</p>
              </div>
              <div className="rounded-md bg-civic-surface px-4 py-3 text-center shadow-sm">
                <div className="text-2xl font-semibold text-civic-heading">{confidence}%</div>
                <div className="text-xs font-semibold uppercase tracking-[0.12em] text-civic-muted">Confidence</div>
              </div>
            </div>
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            <AiResultTile label="Severity" value={prediction.severity} />
            <AiResultTile label="Agency" value={prediction.suggestedAgencyCode} />
            <AiResultTile label="Duplicates" value={duplicates.length ? `${duplicates.length} possible` : "None found"} />
          </div>

          <div className="rounded-md border border-civic-border bg-civic-raised p-4">
            <div className="flex items-center justify-between gap-3">
              <span className="text-sm font-semibold text-civic-heading">Evidence Used</span>
              <span className="text-xs font-semibold text-civic-muted">{prediction.evidence.length} signals</span>
            </div>
            <div className="mt-3 grid gap-2">
              {prediction.evidence.slice(0, 3).map((item) => (
                <div className="rounded-md bg-civic-surface p-3" key={item.id}>
                  <div className="flex items-center justify-between gap-3 text-sm">
                    <span className="font-semibold text-civic-heading">{item.title}</span>
                    <span className="text-civic-primary">{Math.round((item.confidence ?? prediction.confidence) * 100)}%</span>
                  </div>
                  <p className="mt-1 text-sm leading-6 text-civic-muted">{item.detail}</p>
                </div>
              ))}
              {!prediction.evidence.length ? (
                <div className="rounded-md bg-civic-surface p-3 text-sm font-semibold text-civic-muted">
                  The prediction was created from the submitted description and coordinates.
                </div>
              ) : null}
            </div>
          </div>

          {uploadedMedia ? (
            <div className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm">
              <div className="font-semibold text-civic-heading">Media attached</div>
              <p className="mt-1 break-words text-civic-muted">
                {uploadedMedia.fileName} - {uploadedMedia.analysisStatus}
                {uploadedMedia.analysisSummary ? ` - ${uploadedMedia.analysisSummary}` : ""}
              </p>
            </div>
          ) : null}
        </div>
      ) : (
        <div className="rounded-md border border-civic-border bg-civic-raised p-4">
          <div className="flex items-start gap-3">
            <Sparkles className="mt-1 h-5 w-5 shrink-0 text-civic-primary" aria-hidden="true" />
            <div>
              <h3 className="font-semibold text-civic-heading">
                {submissionState === "warning" ? "AI is still processing" : "AI result pending"}
              </h3>
              <p className="mt-2 text-sm leading-6 text-civic-muted">
                Your report was accepted by the backend. The worker will add category, severity, agency routing, duplicate checks, and media analysis when processing completes.
              </p>
            </div>
          </div>
          {status?.steps.length ? (
            <div className="mt-4 grid gap-2">
              {status.steps.slice(0, 4).map((step) => (
                <div className="flex items-center justify-between rounded-md bg-civic-surface px-3 py-2 text-sm" key={step.id}>
                  <span className="font-semibold text-civic-heading">{step.name}</span>
                  <span className="text-civic-muted">{step.status}</span>
                </div>
              ))}
            </div>
          ) : null}
        </div>
      )}
    </Panel>
  );
}

function AiResultTile({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-raised p-3">
      <div className="text-xs font-semibold uppercase tracking-[0.12em] text-civic-muted">{label}</div>
      <div className="mt-1 break-words text-sm font-semibold text-civic-heading">{value}</div>
    </div>
  );
}

function stepDescription(step: (typeof reportSteps)[number]) {
  if (step === "Describe") {
    return "Choose the issue, describe it, and attach media or a voice note.";
  }

  if (step === "Locate") {
    return "Confirm where the issue is so the city can route it correctly.";
  }

  return "Check your summary, update preference, and submit.";
}

function formatSeconds(seconds: number) {
  const minutes = Math.floor(seconds / 60)
    .toString()
    .padStart(2, "0");
  const remainingSeconds = (seconds % 60).toString().padStart(2, "0");

  return `${minutes}:${remainingSeconds}`;
}

function buildIncidentDescription(issueLabel: string, address: string, description: string) {
  const normalizedAddress = address.trim();
  const normalizedDescription = description.trim();

  return normalizedAddress
    ? `[${issueLabel}] ${normalizedDescription} Location note: ${normalizedAddress}.`
    : `[${issueLabel}] ${normalizedDescription}`;
}

function formatGeocodingLabel(result: GeocodingResultDto) {
  const locality = [result.city, result.state].filter(Boolean).join(", ");
  const parts = [result.addressLine, locality].filter(Boolean);

  return parts.length > 0 ? parts.join(" - ") : result.displayName;
}

function formatGeocodingSecondary(result: GeocodingResultDto) {
  const details = [
    result.postalCode,
    result.country,
    `${result.latitude.toFixed(5)}, ${result.longitude.toFixed(5)}`,
  ].filter(Boolean);

  return details.join(" - ");
}

function formatReportCode(value: string) {
  return value.length > 18 ? `${value.slice(0, 8)}...${value.slice(-6)}` : value;
}

function formatQueuedReportId(value: string) {
  return `Queued ${value.slice(0, 8)}`;
}

function formatQueuedReportTime(value: string) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "Saved recently";
  }

  return `Saved ${date.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}`;
}

function shouldQueueAfterSubmissionFailure(error: unknown) {
  return !(error instanceof CivicApiError) || error.status >= 500;
}

function getOfflineQueueErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Could not save this report for offline sync.";
}

function cloneSubmissionProgress() {
  return defaultSubmissionProgress.map((item) => ({ ...item }));
}

function updateSubmissionProgress(
  steps: SubmissionProgressStep[],
  id: SubmissionProgressStep["id"],
  status: SubmissionProgressStepStatus,
  detail: string,
) {
  return steps.map((item) => (item.id === id ? { ...item, detail, status } : item));
}

function failRunningSubmissionProgress(steps: SubmissionProgressStep[], detail: string) {
  const runningStep = steps.find((item) => item.status === "running");

  return runningStep
    ? updateSubmissionProgress(steps, runningStep.id, "error", detail)
    : updateSubmissionProgress(steps, "create", "error", detail);
}

async function waitForBackendTriage(
  trackingCode: string,
  onAttempt: (attempt: number) => void,
): Promise<BackendTriageSnapshot> {
  const maxAttempts = 8;
  let latestStatus: IncidentProcessingStatusDto | null = null;
  let latestPrediction: TriagePredictionDto | null = null;
  let latestDuplicates: DuplicateCandidateDto[] = [];

  for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
    onAttempt(attempt);

    const [statusResult, predictionResult, duplicateResult] = await Promise.all([
      getOptional(() => getPublicIncidentStatus(trackingCode)),
      getOptional(() => getPublicLatestPrediction(trackingCode)),
      getOptional(() => getPublicDuplicateCandidates(trackingCode)),
    ]);

    latestStatus = statusResult;
    latestPrediction = predictionResult;
    latestDuplicates = duplicateResult ?? [];

    if (latestPrediction || hasCompletedProcessing(latestStatus)) {
      return {
        duplicates: latestDuplicates,
        prediction: latestPrediction,
        status: latestStatus,
        timedOut: false,
      };
    }

    if (attempt < maxAttempts) {
      await delay(1_500);
    }
  }

  return {
    duplicates: latestDuplicates,
    prediction: latestPrediction,
    status: latestStatus,
    timedOut: true,
  };
}

function hasCompletedProcessing(status: IncidentProcessingStatusDto | null) {
  if (!status) {
    return false;
  }

  if (status.steps.some((step) => step.status === "InProgress")) {
    return false;
  }

  return ["Triaged", "HumanReviewRequired", "Approved", "Rejected", "NeedsMoreInfo"].includes(status.incidentStatus);
}

function delay(milliseconds: number) {
  return new Promise((resolve) => window.setTimeout(resolve, milliseconds));
}

function submissionStepDotClass(status: SubmissionProgressStepStatus) {
  if (status === "complete") {
    return "bg-status-approved-text";
  }

  if (status === "running") {
    return "bg-civic-primary status-dot";
  }

  if (status === "warning") {
    return "bg-status-review-text";
  }

  if (status === "error") {
    return "bg-status-critical-text";
  }

  if (status === "skipped") {
    return "bg-civic-border-strong";
  }

  return "bg-civic-border";
}

function readReportDraft() {
  if (typeof window === "undefined") {
    return null;
  }

  try {
    const rawDraft = window.localStorage.getItem(reportDraftStorageKey);
    if (!rawDraft) {
      return null;
    }

    const parsed = JSON.parse(rawDraft) as Partial<ReportDraft>;
    const issueType = typeof parsed.issueType === "string" && issueOptions.some((option) => option.id === parsed.issueType)
      ? parsed.issueType
      : defaultReportDraft.issueType;
    const contactPreference = isContactPreference(parsed.contactPreference)
      ? parsed.contactPreference
      : defaultReportDraft.contactPreference;

    return {
      address: typeof parsed.address === "string" ? parsed.address : defaultReportDraft.address,
      contactPreference,
      description: typeof parsed.description === "string" ? parsed.description : defaultReportDraft.description,
      issueType,
      latitude: typeof parsed.latitude === "string" ? parsed.latitude : defaultReportDraft.latitude,
      longitude: typeof parsed.longitude === "string" ? parsed.longitude : defaultReportDraft.longitude,
      updatedAt: typeof parsed.updatedAt === "string" ? parsed.updatedAt : "",
    } satisfies ReportDraft;
  } catch {
    return null;
  }
}

function saveReportDraft(draft: ReportDraft) {
  if (typeof window === "undefined") {
    return;
  }

  try {
    window.localStorage.setItem(reportDraftStorageKey, JSON.stringify(draft));
  } catch {
    // Draft recovery should never block the reporting workflow.
  }
}

function clearStoredReportDraft() {
  if (typeof window === "undefined") {
    return;
  }

  try {
    window.localStorage.removeItem(reportDraftStorageKey);
  } catch {
    // Ignore unavailable storage.
  }
}

function isContactPreference(value: unknown): value is ContactPreference {
  return contactOptions.includes(value as ContactPreference);
}

function formatDraftTime(value: string) {
  if (!value) {
    return "a previous session";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "a previous session";
  }

  return date.toLocaleTimeString([], {
    hour: "numeric",
    minute: "2-digit",
  });
}

function getLocationErrorMessage(error: GeolocationPositionError) {
  if (error.code === error.PERMISSION_DENIED) {
    return "Location permission was denied. Enter the location manually or allow location access in your browser.";
  }

  if (error.code === error.POSITION_UNAVAILABLE) {
    return "The browser could not determine your location. Enter the coordinates manually.";
  }

  if (error.code === error.TIMEOUT) {
    return "Location capture timed out. Try again or enter the coordinates manually.";
  }

  return "Location capture failed. Enter the coordinates manually.";
}

function canPreviewMedia(contentType: string) {
  return contentType.startsWith("image/") || contentType.startsWith("audio/") || contentType.startsWith("video/");
}

function formatEvidenceKind(contentType: string) {
  if (contentType.startsWith("image/")) {
    return "Photo";
  }

  if (contentType.startsWith("audio/")) {
    return "Audio";
  }

  if (contentType.startsWith("video/")) {
    return "Video";
  }

  if (contentType === "application/pdf") {
    return "Document";
  }

  return "File";
}

function getPreferredAudioMimeType() {
  const supportedTypes = ["audio/webm;codecs=opus", "audio/webm", "audio/mp4", "audio/ogg;codecs=opus"];

  return supportedTypes.find((type) => MediaRecorder.isTypeSupported(type)) ?? "";
}

function audioFileExtension(contentType: string) {
  if (contentType.includes("mp4")) {
    return "m4a";
  }

  if (contentType.includes("ogg")) {
    return "ogg";
  }

  if (contentType.includes("wav")) {
    return "wav";
  }

  return "webm";
}

function formatFileSize(bytes: number) {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  const kilobytes = bytes / 1024;
  if (kilobytes < 1024) {
    return `${kilobytes.toFixed(1)} KB`;
  }

  return `${(kilobytes / 1024).toFixed(1)} MB`;
}

function getMicrophoneErrorMessage(error: unknown) {
  if (error instanceof DOMException) {
    if (error.name === "NotAllowedError" || error.name === "SecurityError") {
      return "Microphone permission was denied. Allow microphone access or upload an audio file instead.";
    }

    if (error.name === "NotFoundError" || error.name === "DevicesNotFoundError") {
      return "No microphone was found. Upload an audio file instead.";
    }

    if (error.name === "NotReadableError" || error.name === "TrackStartError") {
      return "The microphone is already in use by another app. Close it and try again.";
    }
  }

  return "Microphone recording failed. Upload an audio file instead.";
}
