import type { IncidentDto, IncidentMediaDto } from "@/lib/civic-api";

export type StoredPublicReport = {
  createdAt: string;
  description: string;
  incidentId: string;
  latitude: number;
  longitude: number;
  mediaFileName?: string | null;
  savedAt: string;
  status: string;
  trackingCode?: string;
};

const publicReportHistoryKey = "civic-signal-public-report-history-v1";
const publicReportHistoryChangedEvent = "civic-signal-public-report-history-changed";
const maxStoredReports = 8;
const emptyPublicReportHistory: StoredPublicReport[] = [];
let cachedRawReports: string | null | undefined;
let cachedReports: StoredPublicReport[] = emptyPublicReportHistory;

export function readPublicReportHistory() {
  if (typeof window === "undefined") {
    return emptyPublicReportHistory;
  }

  try {
    const rawReports = window.localStorage.getItem(publicReportHistoryKey);
    if (rawReports === cachedRawReports) {
      return cachedReports;
    }

    cachedRawReports = rawReports;
    const reports = rawReports ? (JSON.parse(rawReports) as unknown) : [];

    if (!Array.isArray(reports)) {
      cachedReports = emptyPublicReportHistory;
      return cachedReports;
    }

    cachedReports = reports.filter(isStoredPublicReport).slice(0, maxStoredReports);
    return cachedReports;
  } catch {
    cachedReports = emptyPublicReportHistory;
    return cachedReports;
  }
}

export function getPublicReportHistoryServerSnapshot() {
  return emptyPublicReportHistory;
}

export function savePublicReport(report: StoredPublicReport) {
  if (typeof window === "undefined") {
    return;
  }

  const currentReports = readPublicReportHistory();
  const reportKey = getStoredReportKey(report);
  const nextReports = [
    report,
    ...currentReports.filter((item) => getStoredReportKey(item).toLowerCase() !== reportKey.toLowerCase()),
  ].slice(0, maxStoredReports);

  try {
    window.localStorage.setItem(publicReportHistoryKey, JSON.stringify(nextReports));
    cachedRawReports = window.localStorage.getItem(publicReportHistoryKey);
    cachedReports = nextReports;
    emitPublicReportHistoryChanged();
  } catch {
    // Storage can be unavailable in private browsing or locked-down browsers.
  }
}

export function clearPublicReportHistory() {
  if (typeof window === "undefined") {
    return;
  }

  try {
    window.localStorage.removeItem(publicReportHistoryKey);
    cachedRawReports = null;
    cachedReports = emptyPublicReportHistory;
    emitPublicReportHistoryChanged();
  } catch {
    // Ignore unavailable storage.
  }
}

export function subscribePublicReportHistory(onStoreChange: () => void) {
  if (typeof window === "undefined") {
    return () => undefined;
  }

  const handleStorage = (event: StorageEvent) => {
    if (event.key === publicReportHistoryKey) {
      onStoreChange();
    }
  };

  window.addEventListener("storage", handleStorage);
  window.addEventListener(publicReportHistoryChangedEvent, onStoreChange);

  return () => {
    window.removeEventListener("storage", handleStorage);
    window.removeEventListener(publicReportHistoryChangedEvent, onStoreChange);
  };
}

export function createStoredPublicReport(
  incident: IncidentDto,
  options: {
    media?: IncidentMediaDto | null;
    status?: string | null;
  } = {},
): StoredPublicReport {
  return {
    createdAt: incident.createdAt,
    description: incident.description,
    incidentId: incident.id,
    latitude: incident.latitude,
    longitude: incident.longitude,
    mediaFileName: options.media?.fileName ?? null,
    savedAt: new Date().toISOString(),
    status: options.status ?? incident.status,
    trackingCode: incident.trackingCode,
  };
}

function isStoredPublicReport(value: unknown): value is StoredPublicReport {
  if (!value || typeof value !== "object") {
    return false;
  }

  const candidate = value as StoredPublicReport;

  return (
    typeof candidate.createdAt === "string" &&
    typeof candidate.description === "string" &&
    typeof candidate.incidentId === "string" &&
    typeof candidate.latitude === "number" &&
    typeof candidate.longitude === "number" &&
    typeof candidate.savedAt === "string" &&
    typeof candidate.status === "string" &&
    (candidate.trackingCode === undefined || typeof candidate.trackingCode === "string")
  );
}

function getStoredReportKey(report: StoredPublicReport) {
  return report.trackingCode ?? report.incidentId;
}

function emitPublicReportHistoryChanged() {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event(publicReportHistoryChangedEvent));
  }
}
