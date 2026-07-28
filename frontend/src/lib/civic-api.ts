export type ApiProblem = {
  detail?: string;
  status?: number;
  title?: string;
};

export type IncidentDto = {
  acceptedPrediction?: boolean | null;
  assignedAgencyCode?: string | null;
  assignedAt?: string | null;
  assignedByUserId?: string | null;
  assignedTeam?: string | null;
  correctedAgencyCode?: string | null;
  correctedCategory?: string | null;
  correctedSeverity?: string | null;
  createdAt: string;
  dispatchedAt?: string | null;
  dispatchedByUserId?: string | null;
  description: string;
  duplicateLinkedAt?: string | null;
  duplicateLinkedByUserId?: string | null;
  duplicateOfIncidentId?: string | null;
  id: string;
  latitude: number;
  longitude: number;
  notificationAlertsEnabled?: boolean;
  notificationChannel?: string;
  notificationPreferenceUpdatedAt?: string | null;
  reviewDecision?: string | null;
  reviewNote?: string | null;
  reviewedAt?: string | null;
  reviewedByUserId?: string | null;
  status: string;
  trackingCode: string;
};

export type PublicIncidentFeedItemDto = {
  agencyCode?: string | null;
  approximateLatitude: number;
  approximateLongitude: number;
  areaLabel: string;
  category: string;
  createdAt: string;
  description: string;
  hasReview: boolean;
  isDuplicate: boolean;
  latestImageUrl?: string | null;
  latestMediaSummary?: string | null;
  mediaCount: number;
  supportCount: number;
  commentCount: number;
  severity: string;
  status: string;
  trackingCode: string;
};

export type IncidentMediaDto = {
  analysisConfidence?: number | null;
  analysisError?: string | null;
  analysisModelName?: string | null;
  analysisModelVersion?: string | null;
  analysisProcessingTimeMilliseconds?: number | null;
  analysisStatus: string;
  analysisSummary?: string | null;
  analyzedAt?: string | null;
  contentType: string;
  createdAt: string;
  detectedLabels: string[];
  fileName: string;
  id: string;
  incidentId: string;
  mediaType: string;
  storageUri: string;
  transcript?: string | null;
};

export type ProcessingStepDto = {
  completedAt?: string | null;
  errorMessage?: string | null;
  id: string;
  name: string;
  startedAt?: string | null;
  status: string;
  updatedAt: string;
};

export type IncidentProcessingStatusDto = {
  incidentId: string;
  incidentStatus: string;
  steps: ProcessingStepDto[];
};

export type PredictionEvidenceDto = {
  confidence?: number | null;
  createdAt: string;
  detail: string;
  id: string;
  kind: string;
  title: string;
  triagePredictionId: string;
};

export type TriagePredictionDto = {
  category: string;
  confidence: number;
  createdAt: string;
  evidence: PredictionEvidenceDto[];
  id: string;
  incidentId: string;
  modelName: string;
  modelVersion?: string | null;
  processingTimeMilliseconds?: number | null;
  promptVersion?: string | null;
  severity: string;
  suggestedAgencyCode: string;
  summary: string;
};

export type DuplicateCandidateDto = {
  candidateIncidentId: string;
  createdAt: string;
  id: string;
  incidentId: string;
  reason?: string | null;
  similarityScore: number;
  updatedAt: string;
};

export type IncidentUpdateRequestDto = {
  createdAt: string;
  id: string;
  incidentId: string;
  message: string;
  status: string;
};

export type IncidentNotificationPreferenceDto = {
  alertsEnabled: boolean;
  channel: string;
  incidentId: string;
  updatedAt: string;
};

export type IncidentFeedbackDto = {
  comment?: string | null;
  createdAt: string;
  id: string;
  incidentId: string;
  rating: number;
};

export type IncidentReviewDto = {
  acceptedPrediction?: boolean | null;
  correctedAgencyCode?: string | null;
  correctedCategory?: string | null;
  correctedSeverity?: string | null;
  createdAt: string;
  decision: string;
  duplicateOfIncidentId?: string | null;
  id: string;
  incidentId: string;
  note?: string | null;
  reviewerUserId: string;
};

export type IncidentForecastPointDto = {
  actualCount?: number | null;
  date: string;
  forecastCount: number;
  lowerBound: number;
  upperBound: number;
};

export type IncidentForecastDto = {
  explanation: string;
  forecast: IncidentForecastPointDto[];
  generatedOn: string;
  history: IncidentForecastPointDto[];
  historyDays: number;
  horizonDays: number;
  modelName: string;
  modelVersion: string;
  segment: string;
};

export type HistoricalComplaintDto = {
  agency?: string | null;
  agencyName?: string | null;
  borough?: string | null;
  category: string;
  closedAt?: string | null;
  complaintType: string;
  createdAt: string;
  descriptor?: string | null;
  externalId: string;
  id: string;
  importedAt: string;
  incidentAddress?: string | null;
  latitude: number;
  longitude: number;
  resolutionDescription?: string | null;
  source: string;
  status?: string | null;
  updatedAt: string;
};

export type HistoricalComplaintBucketDto = {
  count: number;
  value: string;
};

export type HistoricalComplaintSummaryDto = {
  newestCreatedAt?: string | null;
  oldestCreatedAt?: string | null;
  topAgencies: HistoricalComplaintBucketDto[];
  topBoroughs: HistoricalComplaintBucketDto[];
  topCategories: HistoricalComplaintBucketDto[];
  totalCount: number;
};

export type HistoricalComplaintImportResultDto = {
  createdCount: number;
  importedAt: string;
  receivedCount: number;
  skippedCount: number;
  updatedCount: number;
};

export type AuthTokenResponse = {
  accessToken: string;
  accessTokenExpiresAt: string;
  expiresIn: number;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  refreshTokenExpiresIn: number;
  tokenType: string;
};

type AuthTokenWireResponse = Partial<AuthTokenResponse> & {
  AccessToken?: string;
  AccessTokenExpiresAt?: string;
  ExpiresIn?: number;
  RefreshToken?: string;
  RefreshTokenExpiresAt?: string;
  RefreshTokenExpiresIn?: number;
  TokenType?: string;
};

export type CsrfTokenResponse = {
  headerName: string;
  token: string;
};

export type AuthUserResponse = {
  displayName?: string | null;
  email: string;
  id: string;
  roles: string[];
};

export type DataImportJobDto = {
  createdCount: number;
  errorMessage?: string | null;
  finishedAt?: string | null;
  id: string;
  importType: string;
  parametersJson: string;
  receivedCount: number;
  requestedAt: string;
  requestedByUserId?: string | null;
  skippedCount: number;
  source: string;
  startedAt?: string | null;
  status: string;
  updatedAt: string;
  updatedCount: number;
};

export type BackendCapabilitiesDto = {
  environment: string;
  features: string[];
  generatedAt: string;
  routes: string[];
  service: string;
};

export type SystemIntegrationStatusDto = {
  category: string;
  detail: string;
  enabled: boolean;
  name: string;
  status: string;
};

export type SystemIntegrationStatusResponse = {
  environment: string;
  generatedAt: string;
  integrations: SystemIntegrationStatusDto[];
  service: string;
};

export type SystemRuntimePolicyResponse = {
  aiServiceEnabled: boolean;
  duplicateCandidatePoolSize: number;
  duplicateMaxResults: number;
  duplicateMinimumScore: number;
  duplicateSearchRadiusMeters: number;
  duplicateTimeWindowHours: number;
  geocodingEnabled: boolean;
  maxUploadBytes: number;
  rabbitMqEnabled: boolean;
  redisEnabled: boolean;
  remoteEmbeddingsEnabled: boolean;
  textEmbeddingDimensions: number;
  weatherEnabled: boolean;
};

export type SystemHealthCheckDto = {
  category: string;
  critical: boolean;
  detail: string;
  latencyMilliseconds?: number | null;
  name: string;
  status: string;
};

export type SystemHealthResponse = {
  checks: SystemHealthCheckDto[];
  environment: string;
  generatedAt: string;
  service: string;
  status: string;
};

export type GeocodingResultDto = {
  addressLine?: string | null;
  category?: string | null;
  city?: string | null;
  country?: string | null;
  displayName: string;
  importance?: number | null;
  latitude: number;
  longitude: number;
  postalCode?: string | null;
  state?: string | null;
  type?: string | null;
};

export type AgentToolRunDto = {
  completedAt: string;
  confidence?: number | null;
  inputSummary: string;
  outputSummary: string;
  status: string;
  toolName: string;
};

export type WeatherContextDto = {
  isAvailable: boolean;
  provider: string;
  retrievedAt: string;
  severeAlertSummary?: string | null;
  stationIdentifier?: string | null;
  summary?: string | null;
  temperatureCelsius?: number | null;
  unavailableReason?: string | null;
  windDirection?: string | null;
  windSpeedKph?: number | null;
  precipitationLastHourMillimeters?: number | null;
};

export type DraftWorkOrderDto = {
  agencyCode: string;
  evidence: string[];
  priority: string;
  summary: string;
  title: string;
};

export type ControlledTriageWorkflowDto = {
  draftWorkOrder?: DraftWorkOrderDto | null;
  incidentId: string;
  requiresHumanReview: boolean;
  reviewReason?: string | null;
  slaRisk: number;
  status: string;
  toolRuns: AgentToolRunDto[];
  weather?: WeatherContextDto | null;
};

export type AiEvaluationMetricDto = {
  isHigherBetter: boolean;
  name: string;
  passed: boolean;
  threshold?: number | null;
  unit: string;
  value: number;
};

export type AiEvaluationMetricGroupDto = {
  metrics: AiEvaluationMetricDto[];
  name: string;
  summary: string;
};

export type AiEvaluationGateDto = {
  category: string;
  isHigherBetter: boolean;
  name: string;
  passed: boolean;
  rationale: string;
  threshold: number;
  unit: string;
  value: number;
};

export type AiEvaluationFixtureCountDto = {
  count: number;
  name: string;
};

export type AiModelRunDto = {
  evaluatedAt?: string | null;
  modelVersion: string;
  name: string;
  notes: string;
  provider: string;
  status: string;
};

export type AiEvaluationComparisonDto = {
  baseline: string;
  capability: string;
  decisionRule: string;
  futureTarget: string;
};

export type AiEvaluationBaselineReportDto = {
  baselineName: string;
  comparisons: AiEvaluationComparisonDto[];
  fixtureCounts: AiEvaluationFixtureCountDto[];
  gates: AiEvaluationGateDto[];
  generatedAt: string;
  metricGroups: AiEvaluationMetricGroupDto[];
  modelRuns: AiModelRunDto[];
  nextUpgrades: string[];
  reportVersion: string;
  summary: string;
};

export type ModelLabTokenDto = {
  isStopWord: boolean;
  length: number;
  normalized: string;
  start: number;
  text: string;
  tokenId: number;
};

export type ModelLabEmbeddingFeatureDto = {
  index: number;
  token: string;
  value: number;
};

export type ModelLabClassScoreDto = {
  agencyCode: string;
  category: string;
  evidenceTerms: string[];
  logit: number;
  probability: number;
  severity: string;
};

export type ModelLabAnalysisDto = {
  classScores: ModelLabClassScoreDto[];
  confidence: number;
  embeddingFeatures: ModelLabEmbeddingFeatureDto[];
  embeddingPreview: number[];
  explanation: string;
  input: string;
  modelName: string;
  modelVersion: string;
  normalizedText: string;
  predictedCategory: string;
  severity: string;
  suggestedAgencyCode: string;
  tokens: ModelLabTokenDto[];
};

export type CreateIncidentPayload = {
  description: string;
  latitude: number;
  longitude: number;
};

export type CreateIncidentUpdateRequestPayload = {
  message: string;
};

export type UpdateNotificationPreferencePayload = {
  alertsEnabled: boolean;
  channel?: string | null;
};

export type CreateIncidentFeedbackPayload = {
  comment?: string | null;
  rating: number;
};

export type ReviewIncidentPayload = {
  acceptedPrediction?: boolean | null;
  correctedAgencyCode?: string | null;
  correctedCategory?: string | null;
  correctedSeverity?: string | null;
  decision: string;
  duplicateOfIncidentId?: string | null;
  note?: string | null;
};

export type AssignIncidentPayload = {
  assignedAgencyCode?: string | null;
  assignedTeam: string;
  note?: string | null;
};

export type DispatchIncidentPayload = {
  note?: string | null;
};

export type MarkDuplicateIncidentPayload = {
  duplicateOfIncidentId: string;
  note?: string | null;
};

export type SearchIncidentsOptions = {
  page?: number;
  pageSize?: number;
  status?: string;
};

export type SearchPublicIncidentsOptions = {
  page?: number;
  pageSize?: number;
  status?: string;
};

export type IncidentForecastOptions = {
  agencyCode?: string;
  category?: string;
  historyDays?: number;
  horizonDays?: number;
};

export type SearchHistoricalComplaintsOptions = {
  agency?: string;
  borough?: string;
  category?: string;
  complaintType?: string;
  createdFrom?: string;
  createdTo?: string;
  latitude?: number;
  longitude?: number;
  page?: number;
  pageSize?: number;
  query?: string;
  radiusMeters?: number;
  status?: string;
};

export type ImportNyc311ComplaintsPayload = {
  borough?: string | null;
  complaintType?: string | null;
  daysBack?: number | null;
  limit?: number | null;
};

export type AnalyzeModelLabPayload = {
  embeddingDimensions?: number;
  text: string;
};

export type SearchDataImportJobsOptions = {
  page?: number;
  pageSize?: number;
  source?: string;
  status?: string;
};

const configuredApiBaseUrl = (process.env.NEXT_PUBLIC_API_BASE_URL ?? "").replace(/\/$/, "");
const configuredApiPort = process.env.NEXT_PUBLIC_API_PORT ?? "5020";

export const apiBaseUrl = resolveApiBaseUrl();
const fallbackCsrfHeaderName = "X-CSRF-TOKEN";
let csrfTokenRequest: Promise<CsrfTokenResponse> | null = null;

export class CivicApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
    this.name = "CivicApiError";
  }
}

function resolveApiBaseUrl() {
  if (!configuredApiBaseUrl) {
    return "";
  }

  if (typeof window === "undefined" || !shouldUseBrowserHost(configuredApiBaseUrl)) {
    return configuredApiBaseUrl;
  }

  if (!isPrivateBrowserHost(window.location.hostname)) {
    return "";
  }

  return `${window.location.protocol}//${window.location.hostname}:${configuredApiPort}`;
}

function shouldUseBrowserHost(value: string) {
  try {
    const url = new URL(value);
    const hostname = url.hostname.toLowerCase();

    return hostname === "localhost" || hostname === "127.0.0.1" || hostname === "::1" || hostname === "0.0.0.0";
  } catch {
    return false;
  }
}

function isPrivateBrowserHost(hostname: string) {
  const normalizedHostname = hostname.toLowerCase();
  if (
    normalizedHostname === "localhost"
    || normalizedHostname === "127.0.0.1"
    || normalizedHostname === "::1"
    || normalizedHostname.endsWith(".local")
  ) {
    return true;
  }

  const ipv4Parts = normalizedHostname.split(".").map((part) => Number.parseInt(part, 10));
  if (ipv4Parts.length !== 4 || ipv4Parts.some((part) => !Number.isInteger(part) || part < 0 || part > 255)) {
    return false;
  }

  const [first, second] = ipv4Parts;
  return first === 10
    || (first === 172 && second >= 16 && second <= 31)
    || (first === 192 && second === 168)
    || (first === 169 && second === 254);
}

export function isIncidentId(value: string) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value.trim());
}

export function isTrackingCode(value: string) {
  return /^CS-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$/i.test(value.trim());
}

export async function getCsrfToken() {
  csrfTokenRequest ??= fetch(`${apiBaseUrl}/api/auth/csrf`, {
    credentials: "include",
  })
    .then(async (response) => {
      if (!response.ok) {
        throw new CivicApiError(await readProblemMessage(response), response.status);
      }

      return response.json() as Promise<CsrfTokenResponse>;
    })
    .catch((error) => {
      csrfTokenRequest = null;
      throw error;
    });

  return csrfTokenRequest;
}

export async function getBackendCapabilities() {
  return requestJson<BackendCapabilitiesDto>("/api/system/capabilities");
}

export async function getSystemIntegrations() {
  return requestJson<SystemIntegrationStatusResponse>("/api/system/integrations");
}

export async function getSystemRuntimePolicy() {
  return requestJson<SystemRuntimePolicyResponse>("/api/system/runtime-policy");
}

export async function getSystemHealth() {
  return requestJson<SystemHealthResponse>("/api/system/health");
}

export async function searchLocations(query: string) {
  const params = new URLSearchParams({ query });

  return requestJson<GeocodingResultDto[]>(`/api/geocoding/search?${params}`);
}

export async function reverseGeocode(latitude: number, longitude: number) {
  const params = new URLSearchParams({
    latitude: String(latitude),
    longitude: String(longitude),
  });

  return requestJson<GeocodingResultDto>(`/api/geocoding/reverse?${params}`);
}

export async function login(email: string, password: string) {
  const token = await requestJson<AuthTokenWireResponse>("/api/auth/login", {
    body: JSON.stringify({ email, password }),
    headers: {
      "Content-Type": "application/json",
    },
    method: "POST",
  });

  return normalizeAuthTokenResponse(token);
}

export async function getCurrentUser(accessToken?: string) {
  return requestJson<AuthUserResponse>("/api/auth/me", {
    headers: accessToken
      ? {
          Authorization: `Bearer ${accessToken}`,
        }
      : undefined,
  }, { attemptCookieRefresh: false });
}

export async function refreshAuth(refreshToken?: string) {
  const token = await requestJson<AuthTokenWireResponse>("/api/auth/refresh", {
    body: JSON.stringify({ refreshToken: refreshToken ?? null }),
    headers: {
      "Content-Type": "application/json",
    },
    method: "POST",
  }, { attemptCookieRefresh: false, attachCsrf: false });

  return normalizeAuthTokenResponse(token);
}

export async function logout(refreshToken?: string) {
  return requestJson<void>("/api/auth/logout", {
    body: JSON.stringify({ refreshToken: refreshToken ?? null }),
    headers: {
      "Content-Type": "application/json",
    },
    method: "POST",
  }, { attachCsrf: false });
}

export async function createIncident(payload: CreateIncidentPayload) {
  return requestJson<IncidentDto>("/api/incidents", {
    body: JSON.stringify(payload),
    headers: {
      "Content-Type": "application/json",
    },
    method: "POST",
  });
}

export async function uploadIncidentMedia(incidentId: string, file: File, accessToken?: string) {
  const formData = new FormData();
  formData.append("file", file);

  return requestJson<IncidentMediaDto>(`/api/incidents/${incidentId}/media/upload`, {
    body: formData,
    headers: accessToken
      ? {
          Authorization: `Bearer ${accessToken}`,
        }
      : undefined,
    method: "POST",
  });
}

export async function uploadPublicIncidentMedia(trackingCode: string, file: File) {
  const formData = new FormData();
  formData.append("file", file);

  return requestJson<IncidentMediaDto>(`/api/public/incidents/${encodeURIComponent(trackingCode)}/media/upload`, {
    body: formData,
    method: "POST",
  });
}

export async function getIncident(incidentId: string, accessToken?: string) {
  return requestJson<IncidentDto>(`/api/incidents/${incidentId}`, authorizedRequest(accessToken));
}

export async function getPublicIncident(trackingCode: string) {
  return requestJson<IncidentDto>(`/api/public/incidents/${encodeURIComponent(trackingCode)}`);
}

export async function searchPublicIncidents(options: SearchPublicIncidentsOptions = {}) {
  const params = new URLSearchParams();

  if (options.status) {
    params.set("status", options.status);
  }

  if (options.page) {
    params.set("page", String(options.page));
  }

  if (options.pageSize) {
    params.set("pageSize", String(options.pageSize));
  }

  const queryString = params.toString();

  return requestJson<PublicIncidentFeedItemDto[]>(`/api/public/incidents${queryString ? `?${queryString}` : ""}`);
}

export async function searchIncidents(options: SearchIncidentsOptions = {}, accessToken?: string) {
  const params = new URLSearchParams();

  if (options.status) {
    params.set("status", options.status);
  }

  if (options.page) {
    params.set("page", String(options.page));
  }

  if (options.pageSize) {
    params.set("pageSize", String(options.pageSize));
  }

  const queryString = params.toString();

  return requestJson<IncidentDto[]>(
    `/api/incidents${queryString ? `?${queryString}` : ""}`,
    authorizedRequest(accessToken),
  );
}

export async function getIncidentStatus(incidentId: string, accessToken?: string) {
  return requestJson<IncidentProcessingStatusDto>(`/api/incidents/${incidentId}/status`, authorizedRequest(accessToken));
}

export async function getPublicIncidentStatus(trackingCode: string) {
  return requestJson<IncidentProcessingStatusDto>(`/api/public/incidents/${encodeURIComponent(trackingCode)}/status`);
}

export async function getIncidentMedia(incidentId: string, accessToken?: string) {
  return requestJson<IncidentMediaDto[]>(`/api/incidents/${incidentId}/media`, authorizedRequest(accessToken));
}

export async function getPublicIncidentMedia(trackingCode: string) {
  return requestJson<IncidentMediaDto[]>(`/api/public/incidents/${encodeURIComponent(trackingCode)}/media`);
}

export async function getLatestPrediction(incidentId: string, accessToken?: string) {
  return (await requestJson<TriagePredictionDto | null>(`/api/incidents/${incidentId}/prediction`, authorizedRequest(accessToken))) ?? null;
}

export async function getPublicLatestPrediction(trackingCode: string) {
  return (await requestJson<TriagePredictionDto | null>(`/api/public/incidents/${encodeURIComponent(trackingCode)}/prediction`)) ?? null;
}

export async function getDuplicateCandidates(incidentId: string, accessToken?: string) {
  return requestJson<DuplicateCandidateDto[]>(`/api/incidents/${incidentId}/duplicates`, authorizedRequest(accessToken));
}

export async function runControlledAgentWorkflow(incidentId: string, accessToken?: string) {
  return requestJson<ControlledTriageWorkflowDto>(`/api/incidents/${incidentId}/agent-workflow`, {
    headers: accessToken
      ? {
          Authorization: `Bearer ${accessToken}`,
        }
      : undefined,
    method: "POST",
  });
}

export async function getAiEvaluationBaselines(accessToken?: string) {
  return requestJson<AiEvaluationBaselineReportDto>("/api/ai-evaluations/baselines", authorizedRequest(accessToken));
}

export async function analyzeModelLabText(payload: AnalyzeModelLabPayload) {
  return requestJson<ModelLabAnalysisDto>("/api/model-lab/analyze", {
    body: JSON.stringify(payload),
    headers: {
      "Content-Type": "application/json",
    },
    method: "POST",
  });
}

export async function getPublicDuplicateCandidates(trackingCode: string) {
  return requestJson<DuplicateCandidateDto[]>(`/api/public/incidents/${encodeURIComponent(trackingCode)}/duplicates`);
}

export async function requestIncidentUpdate(incidentId: string, payload: CreateIncidentUpdateRequestPayload, accessToken?: string) {
  return requestJson<IncidentUpdateRequestDto>(`/api/incidents/${incidentId}/update-requests`, {
    body: JSON.stringify(payload),
    headers: authorizedJsonHeadersOrDefault(accessToken),
    method: "POST",
  });
}

export async function requestPublicIncidentUpdate(trackingCode: string, payload: CreateIncidentUpdateRequestPayload) {
  return requestJson<IncidentUpdateRequestDto>(`/api/public/incidents/${encodeURIComponent(trackingCode)}/update-requests`, {
    body: JSON.stringify(payload),
    headers: {
      "Content-Type": "application/json",
    },
    method: "POST",
  });
}

export async function updateNotificationPreference(incidentId: string, payload: UpdateNotificationPreferencePayload, accessToken?: string) {
  return requestJson<IncidentNotificationPreferenceDto>(`/api/incidents/${incidentId}/notification-preference`, {
    body: JSON.stringify(payload),
    headers: authorizedJsonHeadersOrDefault(accessToken),
    method: "PUT",
  });
}

export async function updatePublicNotificationPreference(trackingCode: string, payload: UpdateNotificationPreferencePayload) {
  return requestJson<IncidentNotificationPreferenceDto>(`/api/public/incidents/${encodeURIComponent(trackingCode)}/notification-preference`, {
    body: JSON.stringify(payload),
    headers: {
      "Content-Type": "application/json",
    },
    method: "PUT",
  });
}

export async function submitIncidentFeedback(incidentId: string, payload: CreateIncidentFeedbackPayload, accessToken?: string) {
  return requestJson<IncidentFeedbackDto>(`/api/incidents/${incidentId}/feedback`, {
    body: JSON.stringify(payload),
    headers: authorizedJsonHeadersOrDefault(accessToken),
    method: "POST",
  });
}

export async function submitPublicIncidentFeedback(trackingCode: string, payload: CreateIncidentFeedbackPayload) {
  return requestJson<IncidentFeedbackDto>(`/api/public/incidents/${encodeURIComponent(trackingCode)}/feedback`, {
    body: JSON.stringify(payload),
    headers: {
      "Content-Type": "application/json",
    },
    method: "POST",
  });
}

export async function getPublicIncidentFeedback(trackingCode: string) {
  return requestJson<IncidentFeedbackDto[]>(`/api/public/incidents/${encodeURIComponent(trackingCode)}/feedback`);
}

export async function reviewIncident(incidentId: string, payload: ReviewIncidentPayload, accessToken?: string) {
  return requestJson<IncidentDto>(`/api/incidents/${incidentId}/review`, {
    body: JSON.stringify(payload),
    headers: authorizedJsonHeadersOrDefault(accessToken),
    method: "POST",
  });
}

export async function assignIncident(incidentId: string, payload: AssignIncidentPayload, accessToken?: string) {
  return requestJson<IncidentDto>(`/api/incidents/${incidentId}/assign`, {
    body: JSON.stringify(payload),
    headers: authorizedJsonHeadersOrDefault(accessToken),
    method: "POST",
  });
}

export async function dispatchIncident(incidentId: string, payload: DispatchIncidentPayload = {}, accessToken?: string) {
  return requestJson<IncidentDto>(`/api/incidents/${incidentId}/dispatch`, {
    body: JSON.stringify(payload),
    headers: authorizedJsonHeadersOrDefault(accessToken),
    method: "POST",
  });
}

export async function markIncidentDuplicate(incidentId: string, payload: MarkDuplicateIncidentPayload, accessToken?: string) {
  return requestJson<IncidentDto>(`/api/incidents/${incidentId}/mark-duplicate`, {
    body: JSON.stringify(payload),
    headers: authorizedJsonHeadersOrDefault(accessToken),
    method: "POST",
  });
}

export async function getIncidentReviewHistory(incidentId: string, accessToken?: string) {
  return requestJson<IncidentReviewDto[]>(`/api/incidents/${incidentId}/reviews`, {
    headers: accessToken
      ? {
          Authorization: `Bearer ${accessToken}`,
        }
      : undefined,
  });
}

export async function getIncidentVolumeForecast(options: IncidentForecastOptions = {}) {
  const params = new URLSearchParams();

  if (options.historyDays) {
    params.set("historyDays", String(options.historyDays));
  }

  if (options.horizonDays) {
    params.set("horizonDays", String(options.horizonDays));
  }

  if (options.category) {
    params.set("category", options.category);
  }

  if (options.agencyCode) {
    params.set("agencyCode", options.agencyCode);
  }

  const queryString = params.toString();

  return requestJson<IncidentForecastDto>(`/api/forecasting/incident-volume${queryString ? `?${queryString}` : ""}`);
}

export async function searchHistoricalComplaints(options: SearchHistoricalComplaintsOptions = {}) {
  const queryString = buildHistoricalComplaintQuery(options);

  return requestJson<HistoricalComplaintDto[]>(
    `/api/historical-complaints${queryString ? `?${queryString}` : ""}`,
  );
}

export async function getHistoricalComplaintSummary(options: SearchHistoricalComplaintsOptions = {}) {
  const queryString = buildHistoricalComplaintQuery(options);

  return requestJson<HistoricalComplaintSummaryDto>(
    `/api/historical-complaints/summary${queryString ? `?${queryString}` : ""}`,
  );
}

export async function importNyc311Complaints(payload: ImportNyc311ComplaintsPayload, accessToken?: string) {
  return requestJson<HistoricalComplaintImportResultDto>("/api/historical-complaints/nyc311/import", {
    body: JSON.stringify(payload),
    headers: authorizedJsonHeadersOrDefault(accessToken),
    method: "POST",
  });
}

export async function queueNyc311ImportJob(payload: ImportNyc311ComplaintsPayload, accessToken?: string) {
  return requestJson<DataImportJobDto>("/api/data-import-jobs/nyc311", {
    body: JSON.stringify(payload),
    headers: authorizedJsonHeadersOrDefault(accessToken),
    method: "POST",
  });
}

export async function retryDataImportJob(jobId: string, accessToken?: string) {
  return requestJson<DataImportJobDto>(`/api/data-import-jobs/${jobId}/retry`, {
    headers: accessToken
      ? {
          Authorization: `Bearer ${accessToken}`,
        }
      : undefined,
    method: "POST",
  });
}

export async function searchDataImportJobs(options: SearchDataImportJobsOptions, accessToken?: string) {
  const params = new URLSearchParams();

  if (options.source) {
    params.set("source", options.source);
  }

  if (options.status) {
    params.set("status", options.status);
  }

  if (options.page) {
    params.set("page", String(options.page));
  }

  if (options.pageSize) {
    params.set("pageSize", String(options.pageSize));
  }

  const queryString = params.toString();

  return requestJson<DataImportJobDto[]>(`/api/data-import-jobs${queryString ? `?${queryString}` : ""}`, {
    headers: accessToken
      ? {
          Authorization: `Bearer ${accessToken}`,
        }
      : undefined,
  });
}

export async function getDataImportJob(jobId: string, accessToken?: string) {
  return requestJson<DataImportJobDto>(`/api/data-import-jobs/${jobId}`, {
    headers: accessToken
      ? {
          Authorization: `Bearer ${accessToken}`,
        }
      : undefined,
  });
}

export async function getOptional<T>(loader: () => Promise<T>) {
  try {
    return await loader();
  } catch (error) {
    if (error instanceof CivicApiError && error.status === 404) {
      return null;
    }

    throw error;
  }
}

function authorizedJsonHeaders(accessToken: string) {
  return {
    Authorization: `Bearer ${accessToken}`,
    "Content-Type": "application/json",
  };
}

function authorizedJsonHeadersOrDefault(accessToken?: string) {
  return accessToken
    ? authorizedJsonHeaders(accessToken)
    : {
        "Content-Type": "application/json",
      };
}

function authorizedRequest(accessToken?: string): RequestInit | undefined {
  return accessToken
    ? {
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
      }
    : undefined;
}

function normalizeAuthTokenResponse(token: AuthTokenWireResponse): AuthTokenResponse {
  const accessToken = stringValue(token.accessToken ?? token.AccessToken);
  const refreshToken = stringValue(token.refreshToken ?? token.RefreshToken);
  const expiresIn = numberValue(token.expiresIn ?? token.ExpiresIn) ?? 900;
  const refreshTokenExpiresIn = numberValue(token.refreshTokenExpiresIn ?? token.RefreshTokenExpiresIn) ?? 1_209_600;
  const accessTokenExpiresAt = normalizeDateValue(token.accessTokenExpiresAt ?? token.AccessTokenExpiresAt, expiresIn);
  const refreshTokenExpiresAt = normalizeDateValue(token.refreshTokenExpiresAt ?? token.RefreshTokenExpiresAt, refreshTokenExpiresIn);

  if (!accessToken || !refreshToken) {
    throw new CivicApiError("Authentication response did not include the expected tokens.", 500);
  }

  return {
    accessToken,
    accessTokenExpiresAt,
    expiresIn,
    refreshToken,
    refreshTokenExpiresAt,
    refreshTokenExpiresIn,
    tokenType: stringValue(token.tokenType ?? token.TokenType) || "Bearer",
  };
}

function normalizeDateValue(value: unknown, fallbackSeconds: number) {
  if (typeof value === "string" && Number.isFinite(Date.parse(value))) {
    return value;
  }

  return new Date(Date.now() + fallbackSeconds * 1000).toISOString();
}

function stringValue(value: unknown) {
  return typeof value === "string" ? value : "";
}

function numberValue(value: unknown) {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function buildHistoricalComplaintQuery(options: SearchHistoricalComplaintsOptions) {
  const params = new URLSearchParams();

  for (const [key, value] of Object.entries(options)) {
    if (value !== undefined && value !== null && value !== "") {
      params.set(key, String(value));
    }
  }

  return params.toString();
}

type RequestJsonOptions = {
  attemptCookieRefresh?: boolean;
  attachCsrf?: boolean;
};

async function requestJson<T>(path: string, init?: RequestInit, options: RequestJsonOptions = {}): Promise<T> {
  const requestInit = await buildRequestInit(path, init, options);
  let response = await fetch(`${apiBaseUrl}${path}`, requestInit);

  if (response.status === 401 && options.attemptCookieRefresh !== false && shouldAttemptCookieRefresh(path)) {
    const refreshInit = await buildRequestInit("/api/auth/refresh", {
      body: JSON.stringify({ refreshToken: null }),
      headers: {
        "Content-Type": "application/json",
      },
      method: "POST",
    }, { attemptCookieRefresh: false, attachCsrf: false });
    const refreshResponse = await fetch(`${apiBaseUrl}/api/auth/refresh`, refreshInit);

    if (refreshResponse.ok) {
      response = await fetch(`${apiBaseUrl}${path}`, await buildRequestInit(path, withoutAuthorization(init), options));
    }
  }

  if (!response.ok) {
    throw new CivicApiError(await readProblemMessage(response), response.status);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

async function buildRequestInit(path: string, init?: RequestInit, options: RequestJsonOptions = {}): Promise<RequestInit> {
  const headers = new Headers(init?.headers);

  if (options.attachCsrf !== false && shouldAttachCsrfToken(path, init?.method) && !headers.has("Authorization")) {
    const csrf = await getCsrfToken();
    headers.set(csrf.headerName || fallbackCsrfHeaderName, csrf.token);
  }

  return {
    ...init,
    credentials: "include",
    headers,
  };
}

function shouldAttachCsrfToken(path: string, method = "GET") {
  return isUnsafeMethod(method) && !isCsrfExemptEndpoint(path, method);
}

function isCsrfExemptEndpoint(path: string, method = "GET") {
  return path.startsWith("/api/auth/csrf")
    || path.startsWith("/api/auth/register")
    || path.startsWith("/api/auth/login")
    || path.startsWith("/api/auth/refresh")
    || path.startsWith("/api/auth/logout")
    || path.startsWith("/api/model-lab")
    || path.startsWith("/api/public/")
    || (method.toUpperCase() === "POST" && path === "/api/incidents");
}

function isUnsafeMethod(method: string) {
  const normalizedMethod = method.toUpperCase();
  return normalizedMethod !== "GET"
    && normalizedMethod !== "HEAD"
    && normalizedMethod !== "OPTIONS"
    && normalizedMethod !== "TRACE";
}

function withoutAuthorization(init?: RequestInit): RequestInit | undefined {
  if (!init?.headers) {
    return init;
  }

  const headers = new Headers(init.headers);
  headers.delete("Authorization");

  return {
    ...init,
    headers,
  };
}

function shouldAttemptCookieRefresh(path: string) {
  return !path.startsWith("/api/auth/login")
    && !path.startsWith("/api/auth/refresh")
    && !path.startsWith("/api/auth/logout")
    && !path.startsWith("/api/public/");
}

async function readProblemMessage(response: Response) {
  try {
    const problem = (await response.json()) as ApiProblem;
    return problem.detail || problem.title || `Request failed with status ${response.status}`;
  } catch {
    return `Request failed with status ${response.status}`;
  }
}
