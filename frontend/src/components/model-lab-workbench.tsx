"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Binary,
  BrainCircuit,
  CheckCircle2,
  GitCompareArrows,
  Loader2,
  Network,
  Play,
  RotateCcw,
  Sparkles,
} from "lucide-react";
import {
  analyzeModelLabText,
  CivicApiError,
  type ModelLabAnalysisDto,
  type ModelLabClassScoreDto,
} from "@/lib/civic-api";
import { fieldClassName, MetricCard, PageHeader, Panel, ScoreBar } from "@/components/ui-kit";

const samples = [
  "Large pothole on Pine Street forcing cars to swerve near the bus stop.",
  "Blocked storm drain after heavy rain and water is pooling across the crosswalk.",
  "Streetlight is dark near the school entrance and the traffic signal is flickering.",
  "Trash bags and construction debris were dumped beside the alley overnight.",
] as const;

type LoadState = "idle" | "loading" | "ready" | "error";

export function ModelLabWorkbench() {
  const [text, setText] = useState<string>(samples[0]);
  const [analysis, setAnalysis] = useState<ModelLabAnalysisDto | null>(null);
  const [state, setState] = useState<LoadState>("idle");
  const [message, setMessage] = useState("Analyze a complaint to inspect the classifier internals.");

  const topScores = useMemo(() => analysis?.classScores.slice(0, 4) ?? [], [analysis]);
  const meaningfulTokens = useMemo(() => analysis?.tokens.filter((token) => !token.isStopWord) ?? [], [analysis]);

  const analyzeComplaint = useCallback(async (nextText: string) => {
    const normalized = nextText.trim();

    if (normalized.length < 5) {
      setState("error");
      setMessage("Enter a longer complaint before analyzing.");
      return;
    }

    setState("loading");
    setMessage("Running tokenization, hashing embeddings, logits, and softmax...");

    try {
      const result = await analyzeModelLabText({
        embeddingDimensions: 16,
        text: normalized,
      });
      setAnalysis(result);
      setState("ready");
      setMessage(`${result.modelName} predicted ${result.predictedCategory} with ${Math.round(result.confidence * 100)}% confidence.`);
    } catch (error) {
      setState("error");
      setMessage(error instanceof CivicApiError ? error.message : "Model Lab API is unavailable.");
    }
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void analyzeComplaint(samples[0]);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [analyzeComplaint]);

  return (
    <div className="space-y-6">
      <PageHeader
        actions={
          <button
            className="inline-flex h-11 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-sm font-semibold text-white transition hover:bg-civic-primary-strong disabled:cursor-not-allowed disabled:opacity-60"
            disabled={state === "loading"}
            onClick={() => void analyzeComplaint(text)}
            type="button"
          >
            {state === "loading" ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <Play className="h-4 w-4" aria-hidden="true" />}
            Analyze
          </button>
        }
        description="Inspect how a transparent baseline classifier turns complaint text into tokens, IDs, embeddings, logits, probabilities, and a routing decision."
        eyebrow="AI Learning Lab"
        title="Model Lab"
      />

      <div
        className={`rounded-lg border p-4 text-sm font-semibold ${
          state === "error"
            ? "border-status-critical bg-status-critical/10 text-status-critical-text"
            : state === "ready"
              ? "border-status-approved bg-status-approved/10 text-status-approved-text"
              : "border-civic-border bg-civic-surface text-civic-muted"
        }`}
      >
        {message}
      </div>

      <section className="grid gap-6 xl:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
        <Panel title="Complaint Input" description="Try different city-service descriptions and rerun the classifier.">
          <textarea
            className={`${fieldClassName} min-h-40 resize-y leading-7`}
            onChange={(event) => setText(event.target.value)}
            value={text}
          />
          <div className="mt-4 grid gap-2 sm:grid-cols-2">
            {samples.map((sample) => (
              <button
                className="rounded-md border border-civic-border bg-civic-raised p-3 text-left text-sm font-semibold text-civic-muted transition hover:border-civic-primary hover:bg-civic-soft hover:text-civic-primary"
                key={sample}
                onClick={() => {
                  setText(sample);
                  void analyzeComplaint(sample);
                }}
                type="button"
              >
                {sample}
              </button>
            ))}
          </div>
        </Panel>

        <div className="grid gap-4 sm:grid-cols-3">
          <MetricCard
            icon={<BrainCircuit className="h-5 w-5" />}
            label="Predicted category"
            trend={analysis?.suggestedAgencyCode ?? "Waiting for analysis"}
            value={analysis?.predictedCategory ?? "-"}
          />
          <MetricCard
            icon={<CheckCircle2 className="h-5 w-5" />}
            label="Confidence"
            tone="calm"
            trend={analysis?.severity ?? "Softmax top probability"}
            value={analysis ? `${Math.round(analysis.confidence * 100)}%` : "-"}
          />
          <MetricCard
            icon={<Binary className="h-5 w-5" />}
            label="Tokens"
            trend={`${meaningfulTokens.length} signal tokens`}
            value={String(analysis?.tokens.length ?? 0)}
          />
          <Panel className="sm:col-span-3" title="Classifier Decision" description={analysis?.modelVersion ?? "Model version appears after analysis."}>
            {analysis ? (
              <div className="grid gap-4">
                <p className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm font-semibold leading-6 text-civic-heading">
                  {analysis.explanation}
                </p>
                <div className="grid gap-3">
                  {topScores.map((score) => (
                    <ClassScoreBar key={score.category} score={score} />
                  ))}
                </div>
              </div>
            ) : (
              <EmptyState label="Run the classifier to see logits and probabilities." />
            )}
          </Panel>
        </div>
      </section>

      <section className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
        <Panel title="Tokenization" description="Each token gets normalized and mapped to a stable integer ID before features are created.">
          {analysis ? (
            <div className="overflow-hidden rounded-md border border-civic-border">
              <div className="grid grid-cols-[1.2fr_1fr_0.8fr] bg-civic-raised px-3 py-2 text-xs font-semibold uppercase tracking-[0.12em] text-civic-muted">
                <span>Token</span>
                <span>Normalized</span>
                <span>ID</span>
              </div>
              <div className="max-h-80 overflow-y-auto">
                {analysis.tokens.map((token) => (
                  <div
                    className={`grid grid-cols-[1.2fr_1fr_0.8fr] border-t border-civic-border px-3 py-2 text-sm ${
                      token.isStopWord ? "bg-civic-raised/50 text-civic-muted" : "bg-civic-surface text-civic-heading"
                    }`}
                    key={`${token.start}-${token.text}`}
                  >
                    <span className="font-semibold">{token.text}</span>
                    <span>{token.normalized}</span>
                    <span>{token.tokenId}</span>
                  </div>
                ))}
              </div>
            </div>
          ) : (
            <EmptyState label="No tokens yet." />
          )}
        </Panel>

        <Panel title="Embedding Preview" description="The backend baseline uses hashing-trick features so vectors stay deterministic and easy to explain.">
          {analysis ? (
            <div className="grid gap-5">
              <div className="grid grid-cols-8 gap-2">
                {analysis.embeddingPreview.map((value, index) => (
                  <div className="rounded-md border border-civic-border bg-civic-raised p-2 text-center" key={index}>
                    <div className="text-xs font-semibold text-civic-muted">d{index}</div>
                    <div className="mt-1 text-sm font-semibold text-civic-heading">{value.toFixed(2)}</div>
                  </div>
                ))}
              </div>
              <div className="grid gap-2">
                {analysis.embeddingFeatures.map((feature) => (
                  <div className="grid grid-cols-[1fr_auto_auto] items-center gap-3 rounded-md border border-civic-border bg-civic-raised px-3 py-2 text-sm" key={`${feature.token}-${feature.index}`}>
                    <span className="font-semibold text-civic-heading">{feature.token}</span>
                    <span className="text-civic-muted">d{feature.index}</span>
                    <span className={feature.value >= 0 ? "text-status-approved-text" : "text-status-critical-text"}>
                      {feature.value > 0 ? "+" : ""}
                      {feature.value}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          ) : (
            <EmptyState label="No embedding preview yet." />
          )}
        </Panel>
      </section>

      <Panel title="Pipeline View" description="The same complaint moves through deterministic model stages before the backend creates a routing recommendation.">
        <div className="grid gap-3 md:grid-cols-4">
          {[
            { icon: Binary, label: "Tokens", value: `${analysis?.tokens.length ?? 0} pieces` },
            { icon: Network, label: "Embedding", value: `${analysis?.embeddingPreview.length ?? 0} dimensions` },
            { icon: GitCompareArrows, label: "Softmax", value: `${analysis?.classScores.length ?? 0} classes` },
            { icon: Sparkles, label: "Decision", value: analysis?.suggestedAgencyCode ?? "Pending" },
          ].map((item) => (
            <div className="rounded-lg border border-civic-border bg-civic-raised p-4" key={item.label}>
              <item.icon className="h-5 w-5 text-civic-primary" aria-hidden="true" />
              <div className="mt-3 text-sm font-semibold text-civic-heading">{item.label}</div>
              <div className="mt-1 text-sm text-civic-muted">{item.value}</div>
            </div>
          ))}
        </div>
      </Panel>
    </div>
  );
}

function ClassScoreBar({ score }: { score: ModelLabClassScoreDto }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-raised p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <div className="font-semibold text-civic-heading">{score.category}</div>
          <div className="text-sm text-civic-muted">
            logit {score.logit.toFixed(2)} - {score.agencyCode} - {score.severity}
          </div>
        </div>
        <span className="rounded-md bg-civic-soft px-2 py-1 text-sm font-semibold text-civic-primary">
          {Math.round(score.probability * 100)}%
        </span>
      </div>
      <div className="mt-3">
        <ScoreBar score={score.probability * 100} />
      </div>
      {score.evidenceTerms.length > 0 ? (
        <div className="mt-3 flex flex-wrap gap-2">
          {score.evidenceTerms.map((term) => (
            <span className="rounded-md bg-civic-surface px-2 py-1 text-xs font-semibold text-civic-muted" key={term}>
              {term}
            </span>
          ))}
        </div>
      ) : null}
    </div>
  );
}

function EmptyState({ label }: { label: string }) {
  return (
    <div className="grid min-h-40 place-items-center rounded-md border border-civic-border bg-civic-raised p-5 text-center text-sm font-semibold text-civic-muted">
      <span className="inline-flex items-center gap-2">
        <RotateCcw className="h-4 w-4" aria-hidden="true" />
        {label}
      </span>
    </div>
  );
}
