"use client";

import { useMemo, useState } from "react";
import type { ReactNode } from "react";
import { AlertTriangle, CheckCircle2, ChevronDown, Droplets, MapPinned, Road, SearchCheck, TrafficCone } from "lucide-react";
import { PageHeader, Panel, ScoreBar, SegmentedControl } from "@/components/ui-kit";

const topics = ["All", "Roads", "Drainage", "Signals", "Status"] as const;
const urgencyLevels = ["Routine", "Safety hazard", "Immediate danger"] as const;
const evidenceLevels = ["Need photo", "Photo ready", "Location only"] as const;

const faqs = [
  {
    topic: "Roads",
    question: "What should I include when reporting a pothole?",
    answer: "Include the closest intersection, lane direction if known, size estimate, and a photo when it is safe to take one.",
  },
  {
    topic: "Drainage",
    question: "When should I report blocked drains?",
    answer: "Report blocked drains when water is pooling, entering sidewalks, blocking ramps, or creating unsafe road conditions.",
  },
  {
    topic: "Signals",
    question: "Are traffic signal outages prioritized?",
    answer: "Yes. Signal outages and safety hazards are routed with higher urgency and can be escalated to dispatch.",
  },
  {
    topic: "Status",
    question: "Can I track my report?",
    answer: "Yes. Use the report code from your confirmation screen on the Status page.",
  },
];

export function PublicHelpCenter() {
  const [topic, setTopic] = useState<(typeof topics)[number]>("All");
  const [openQuestion, setOpenQuestion] = useState(faqs[0].question);
  const [urgency, setUrgency] = useState<(typeof urgencyLevels)[number]>("Routine");
  const [evidence, setEvidence] = useState<(typeof evidenceLevels)[number]>("Photo ready");

  const visibleFaqs = useMemo(() => {
    return topic === "All" ? faqs : faqs.filter((faq) => faq.topic === topic);
  }, [topic]);
  const readiness = Math.min(
    100,
    (topic === "All" ? 20 : 40) + (urgency === "Immediate danger" ? 10 : urgency === "Safety hazard" ? 28 : 22) + (evidence === "Photo ready" ? 32 : 18),
  );
  const guidance = getGuidance(urgency, evidence);

  return (
    <div className="space-y-6">
      <PageHeader
        description="Quick guidance for residents before submitting or tracking a city service request."
        eyebrow="Resident Portal"
        title="Reporting Help"
      />

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <HelpTile icon={<Road className="h-5 w-5" />} label="Road damage" value="Potholes, cracks, unsafe surfaces" />
        <HelpTile icon={<Droplets className="h-5 w-5" />} label="Drainage" value="Blocked drains, pooling, flooding" />
        <HelpTile icon={<TrafficCone className="h-5 w-5" />} label="Signals" value="Dark lights, timing, damaged signs" />
        <HelpTile icon={<MapPinned className="h-5 w-5" />} label="Location" value="Intersection, landmark, or GPS" />
      </div>

      <Panel title="Report Readiness Check" description={guidance}>
        <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_260px]">
          <div className="grid gap-4">
            <div>
              <div className="mb-2 text-sm font-semibold text-civic-heading">Issue area</div>
              <SegmentedControl onChange={setTopic} options={topics} value={topic} />
            </div>
            <div>
              <div className="mb-2 text-sm font-semibold text-civic-heading">Situation</div>
              <div className="grid gap-2 sm:grid-cols-3">
                {urgencyLevels.map((option) => (
                  <button
                    aria-pressed={urgency === option}
                    className={`h-11 rounded-md border px-3 text-sm font-semibold transition ${
                      urgency === option
                        ? "border-civic-primary bg-civic-primary text-white"
                        : "border-civic-border bg-civic-raised text-civic-muted hover:bg-civic-soft hover:text-civic-primary"
                    }`}
                    key={option}
                    onClick={() => setUrgency(option)}
                    type="button"
                  >
                    {option}
                  </button>
                ))}
              </div>
            </div>
            <div>
              <div className="mb-2 text-sm font-semibold text-civic-heading">Evidence</div>
              <div className="grid gap-2 sm:grid-cols-3">
                {evidenceLevels.map((option) => (
                  <button
                    aria-pressed={evidence === option}
                    className={`h-11 rounded-md border px-3 text-sm font-semibold transition ${
                      evidence === option
                        ? "border-civic-primary bg-civic-primary text-white"
                        : "border-civic-border bg-civic-raised text-civic-muted hover:bg-civic-soft hover:text-civic-primary"
                    }`}
                    key={option}
                    onClick={() => setEvidence(option)}
                    type="button"
                  >
                    {option}
                  </button>
                ))}
              </div>
            </div>
          </div>
          <div className="rounded-md border border-civic-border bg-civic-raised p-4">
            <SearchCheck className="h-6 w-6 text-civic-primary" aria-hidden="true" />
            <div className="mt-4">
              <ScoreBar label="Report readiness" score={readiness} />
            </div>
            <div className="mt-4 flex items-center gap-2 rounded-md bg-civic-soft p-3 text-sm font-semibold text-civic-primary">
              <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
              {topic === "All" ? "Pick an issue area" : `${topic} selected`}
            </div>
          </div>
        </div>
      </Panel>

      <Panel
        action={<SegmentedControl onChange={setTopic} options={topics} value={topic} />}
        title="Frequently Asked Questions"
        description="Filter by topic and expand the question you need."
      >
        <div className="grid gap-3">
          {visibleFaqs.map((faq) => {
            const open = openQuestion === faq.question;

            return (
              <button
                aria-expanded={open}
                className={`rounded-md border p-4 text-left transition ${
                  open ? "border-civic-primary bg-civic-soft" : "border-civic-border bg-civic-raised hover:border-civic-border-strong"
                }`}
                key={faq.question}
                onClick={() => setOpenQuestion(open ? "" : faq.question)}
                type="button"
              >
                <div className="flex items-center justify-between gap-4">
                  <span className="font-semibold text-civic-heading">{faq.question}</span>
                  <ChevronDown className={`h-4 w-4 text-civic-primary transition ${open ? "rotate-180" : ""}`} aria-hidden="true" />
                </div>
                {open ? <p className="mt-3 text-sm leading-6 text-civic-muted">{faq.answer}</p> : null}
              </button>
            );
          })}
        </div>
      </Panel>

      <Panel title="Safety Note" description="Some incidents need immediate emergency attention.">
        <div className="rounded-md border border-status-critical bg-status-critical p-4 text-sm leading-6 text-status-critical-text">
          <div className="flex items-center gap-2 font-semibold">
            <AlertTriangle className="h-4 w-4" aria-hidden="true" />
            For emergencies or immediate danger, contact emergency services instead of submitting a routine city report.
          </div>
        </div>
      </Panel>
    </div>
  );
}

function getGuidance(urgency: (typeof urgencyLevels)[number], evidence: (typeof evidenceLevels)[number]) {
  if (urgency === "Immediate danger") {
    return "Immediate danger should be handled through emergency services first.";
  }

  if (evidence === "Need photo") {
    return "A report can still be submitted, but a photo helps staff confirm the issue faster.";
  }

  if (urgency === "Safety hazard") {
    return "Safety hazards should include the closest landmark and clear location details.";
  }

  return "This looks ready for a routine city service report.";
}

function HelpTile({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  return (
    <div className="animate-fade-up rounded-lg border border-civic-border bg-civic-surface p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-civic-border-strong">
      <span className="inline-flex rounded-md bg-civic-soft p-2 text-civic-primary">{icon}</span>
      <h2 className="mt-4 text-lg font-semibold text-civic-heading">{label}</h2>
      <p className="mt-2 text-sm leading-6 text-civic-muted">{value}</p>
    </div>
  );
}
