import {
  createIncident,
  uploadPublicIncidentMedia,
  type CreateIncidentPayload,
  type IncidentDto,
  type IncidentMediaDto,
} from "@/lib/civic-api";
import { createStoredPublicReport, savePublicReport } from "@/lib/public-report-history";

const databaseName = "civic-signal-offline-reports";
const databaseVersion = 1;
const storeName = "queued-reports";
const queueChangedEvent = "civic-signal-offline-report-queue-changed";

type QueuedPublicReportStatus = "Queued" | "Syncing" | "Failed";

type QueuedPublicReportRecord = {
  address: string;
  attempts: number;
  contactPreference: string;
  createdAt: string;
  description: string;
  id: string;
  issueType: string;
  lastAttemptAt?: string | null;
  lastError?: string | null;
  latitude: number;
  longitude: number;
  mediaFile?: File | null;
  status: QueuedPublicReportStatus;
};

export type QueuedPublicReportSummary = Omit<QueuedPublicReportRecord, "mediaFile"> & {
  hasMedia: boolean;
  mediaContentType?: string | null;
  mediaFileName?: string | null;
  mediaSize?: number | null;
};

export type EnqueuePublicReportInput = {
  address: string;
  contactPreference: string;
  description: string;
  issueType: string;
  latitude: number;
  longitude: number;
  mediaFile?: File | null;
};

export type OfflineReportSyncEvent =
  | {
      incident?: undefined;
      queuedReport: QueuedPublicReportSummary;
      type: "syncing";
    }
  | {
      incident: IncidentDto;
      media?: IncidentMediaDto | null;
      queuedReport: QueuedPublicReportSummary;
      type: "synced";
    }
  | {
      error: string;
      queuedReport: QueuedPublicReportSummary;
      type: "failed";
    };

export type OfflineReportSyncResult = {
  attempted: number;
  completed: number;
  failed: number;
};

export function isOfflineReportQueueSupported() {
  return typeof window !== "undefined" && "indexedDB" in window;
}

export async function enqueuePublicReport(input: EnqueuePublicReportInput) {
  const record: QueuedPublicReportRecord = {
    address: input.address,
    attempts: 0,
    contactPreference: input.contactPreference,
    createdAt: new Date().toISOString(),
    description: input.description,
    id: crypto.randomUUID(),
    issueType: input.issueType,
    latitude: input.latitude,
    longitude: input.longitude,
    mediaFile: input.mediaFile ?? null,
    status: "Queued",
  };

  await putQueuedReport(record);
  emitQueueChanged();

  return toSummary(record);
}

export async function listQueuedPublicReports() {
  const records = await listQueuedReportRecords();
  return records.map(toSummary);
}

export async function removeQueuedPublicReport(id: string) {
  const database = await openQueueDatabase();

  try {
    const transaction = database.transaction(storeName, "readwrite");
    transaction.objectStore(storeName).delete(id);
    await transactionDone(transaction);
  } finally {
    database.close();
  }

  emitQueueChanged();
}

export async function syncQueuedPublicReports({
  onProgress,
}: {
  onProgress?: (event: OfflineReportSyncEvent) => void;
} = {}): Promise<OfflineReportSyncResult> {
  const records = await listQueuedReportRecords();
  let completed = 0;
  let failed = 0;

  for (const record of records) {
    const syncingRecord: QueuedPublicReportRecord = {
      ...record,
      lastAttemptAt: new Date().toISOString(),
      status: "Syncing",
    };
    await putQueuedReport(syncingRecord);
    onProgress?.({ queuedReport: toSummary(syncingRecord), type: "syncing" });

    try {
      const incident = await createIncident(toCreateIncidentPayload(record));
      const media = await uploadQueuedMedia(incident.trackingCode, record.mediaFile ?? null);

      savePublicReport(
        createStoredPublicReport(incident, {
          media,
          status: incident.status,
        }),
      );

      await removeQueuedPublicReport(record.id);
      completed += 1;
      onProgress?.({
        incident,
        media,
        queuedReport: toSummary(record),
        type: "synced",
      });
    } catch (error) {
      failed += 1;
      const failedRecord: QueuedPublicReportRecord = {
        ...record,
        attempts: record.attempts + 1,
        lastAttemptAt: new Date().toISOString(),
        lastError: getSyncErrorMessage(error),
        status: "Failed",
      };

      await putQueuedReport(failedRecord);
      onProgress?.({
        error: failedRecord.lastError ?? "Sync failed.",
        queuedReport: toSummary(failedRecord),
        type: "failed",
      });
    }
  }

  emitQueueChanged();

  return {
    attempted: records.length,
    completed,
    failed,
  };
}

export function subscribeOfflineReportQueue(onStoreChange: () => void) {
  if (typeof window === "undefined") {
    return () => undefined;
  }

  window.addEventListener(queueChangedEvent, onStoreChange);

  return () => {
    window.removeEventListener(queueChangedEvent, onStoreChange);
  };
}

function toCreateIncidentPayload(record: QueuedPublicReportRecord): CreateIncidentPayload {
  return {
    description: record.description,
    latitude: record.latitude,
    longitude: record.longitude,
  };
}

async function uploadQueuedMedia(trackingCode: string, mediaFile: File | null) {
  if (!mediaFile) {
    return null;
  }

  return uploadPublicIncidentMedia(trackingCode, mediaFile);
}

async function listQueuedReportRecords() {
  const database = await openQueueDatabase();

  try {
    const transaction = database.transaction(storeName, "readonly");
    const records = await requestToPromise<QueuedPublicReportRecord[]>(
      transaction.objectStore(storeName).getAll(),
    );
    await transactionDone(transaction);

    return records.sort((left, right) => left.createdAt.localeCompare(right.createdAt));
  } finally {
    database.close();
  }
}

async function putQueuedReport(record: QueuedPublicReportRecord) {
  const database = await openQueueDatabase();

  try {
    const transaction = database.transaction(storeName, "readwrite");
    transaction.objectStore(storeName).put(record);
    await transactionDone(transaction);
  } finally {
    database.close();
  }
}

function openQueueDatabase() {
  return new Promise<IDBDatabase>((resolve, reject) => {
    if (!isOfflineReportQueueSupported()) {
      reject(new Error("Offline report queue is not supported in this browser."));
      return;
    }

    const request = indexedDB.open(databaseName, databaseVersion);

    request.onupgradeneeded = () => {
      const database = request.result;

      if (!database.objectStoreNames.contains(storeName)) {
        const store = database.createObjectStore(storeName, { keyPath: "id" });
        store.createIndex("createdAt", "createdAt", { unique: false });
        store.createIndex("status", "status", { unique: false });
      }
    };

    request.onerror = () => reject(request.error ?? new Error("Could not open offline report queue."));
    request.onsuccess = () => resolve(request.result);
  });
}

function requestToPromise<T>(request: IDBRequest<T>) {
  return new Promise<T>((resolve, reject) => {
    request.onerror = () => reject(request.error ?? new Error("IndexedDB request failed."));
    request.onsuccess = () => resolve(request.result);
  });
}

function transactionDone(transaction: IDBTransaction) {
  return new Promise<void>((resolve, reject) => {
    transaction.onabort = () => reject(transaction.error ?? new Error("IndexedDB transaction aborted."));
    transaction.onerror = () => reject(transaction.error ?? new Error("IndexedDB transaction failed."));
    transaction.oncomplete = () => resolve();
  });
}

function toSummary(record: QueuedPublicReportRecord): QueuedPublicReportSummary {
  return {
    address: record.address,
    attempts: record.attempts,
    contactPreference: record.contactPreference,
    createdAt: record.createdAt,
    description: record.description,
    hasMedia: Boolean(record.mediaFile),
    id: record.id,
    issueType: record.issueType,
    lastAttemptAt: record.lastAttemptAt,
    lastError: record.lastError,
    latitude: record.latitude,
    longitude: record.longitude,
    mediaContentType: record.mediaFile?.type ?? null,
    mediaFileName: record.mediaFile?.name ?? null,
    mediaSize: record.mediaFile?.size ?? null,
    status: record.status,
  };
}

function getSyncErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Could not sync queued report.";
}

function emitQueueChanged() {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event(queueChangedEvent));
  }
}
