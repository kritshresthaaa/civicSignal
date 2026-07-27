import { HubConnectionBuilder, type ILogger } from "@microsoft/signalr";
import {
  apiBaseUrl,
  type DuplicateCandidateDto,
  type IncidentDto,
  type IncidentMediaDto,
  type IncidentProcessingStatusDto,
  type TriagePredictionDto,
} from "@/lib/civic-api";

export type IncidentRealtimeEventDto = {
  duplicateCandidates: DuplicateCandidateDto[];
  eventType: string;
  incident?: IncidentDto | null;
  incidentId: string;
  incidentStatus: string;
  media?: IncidentMediaDto | null;
  message: string;
  occurredAt: string;
  prediction?: TriagePredictionDto | null;
  processingStatus: IncidentProcessingStatusDto;
};

export const incidentRealtimeEvents = {
  incidentUpdated: "incidentUpdated",
  operationsIncidentUpdated: "operationsIncidentUpdated",
} as const;

export const incidentRealtimeEventTypes = {
  analyzed: "incident.analyzed",
  assigned: "incident.assigned",
  created: "incident.created",
  dispatched: "incident.dispatched",
  duplicateLinked: "incident.duplicateLinked",
  feedbackReceived: "incident.feedbackReceived",
  mediaAnalyzed: "incident.mediaAnalyzed",
  mediaAdded: "incident.mediaAdded",
  notificationPreferenceUpdated: "incident.notificationPreferenceUpdated",
  processingStatusChanged: "incident.processingStatusChanged",
  reviewed: "incident.reviewed",
  updateRequested: "incident.updateRequested",
} as const;

export type RealtimeConnectionState = "idle" | "connecting" | "connected" | "reconnecting" | "offline";

const silentSignalRLogger: ILogger = {
  log: () => undefined,
};

export async function isIncidentRealtimeAvailable(timeoutMs = 1200) {
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), timeoutMs);

  try {
    const response = await fetch(`${apiBaseUrl}/hubs/incidents/negotiate?negotiateVersion=1`, {
      credentials: "omit",
      method: "POST",
      signal: controller.signal,
    });

    return response.ok;
  } catch {
    return false;
  } finally {
    window.clearTimeout(timeout);
  }
}

export function createIncidentHubConnection(accessToken?: string) {
  return new HubConnectionBuilder()
    .withUrl(`${apiBaseUrl}/hubs/incidents`, {
      accessTokenFactory: accessToken ? () => accessToken : undefined,
      withCredentials: true,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(silentSignalRLogger)
    .build();
}
