export type IncidentStatus = "Submitted" | "Triaged" | "HumanReviewRequired" | "Approved" | "Dispatched";

export type Severity = "Low" | "Medium" | "High" | "Critical";

export type EvidenceItem = {
  title: string;
  detail: string;
  confidence: number;
};

export type DuplicateCandidate = {
  caseId: string;
  distanceMeters: number;
  score: number;
};

export type TimelineEvent = {
  label: string;
  time: string;
  detail: string;
};

export type IncidentRow = {
  id: string;
  title: string;
  description: string;
  category: string;
  agency: string;
  assignedTeam: string;
  status: IncidentStatus;
  severity: Severity;
  confidence: number;
  age: string;
  location: string;
  cityZone: number;
  slaRisk: number;
  channel: "Mobile" | "Web" | "Call Center" | "Field Crew";
  reporter: string;
  coordinates: {
    latitude: number;
    longitude: number;
  };
  aiSummary: string;
  evidence: EvidenceItem[];
  duplicates: DuplicateCandidate[];
  timeline: TimelineEvent[];
};
