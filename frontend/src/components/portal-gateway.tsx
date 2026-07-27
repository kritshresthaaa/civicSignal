import Link from "next/link";
import type { ReactNode } from "react";
import { BarChart3, BrainCircuit, Building2, ClipboardList, ListChecks, SearchCheck, ShieldCheck, Sparkles } from "lucide-react";
import { Panel } from "@/components/ui-kit";

export function PortalGateway() {
  return (
    <main className="min-h-screen bg-civic-page text-civic-ink">
      <div className="mx-auto flex min-h-screen max-w-7xl flex-col px-4 py-6 sm:px-6 lg:px-8">
        <header className="flex items-center justify-between border-b border-civic-border pb-5">
          <Link className="flex items-center gap-3" href="/">
            <span className="scan-line flex h-11 w-11 items-center justify-center rounded-lg bg-civic-primary text-white">
              <Sparkles className="h-5 w-5" aria-hidden="true" />
            </span>
            <span>
              <span className="block text-lg font-semibold text-civic-heading">CivicSignal AI</span>
              <span className="block text-sm text-civic-muted">Public and operations portals</span>
            </span>
          </Link>
          <Link
            className="hidden h-10 items-center gap-2 rounded-md border border-civic-border px-3 text-sm font-semibold text-civic-primary hover:bg-civic-soft sm:inline-flex"
            href="/admin/dashboard"
          >
            <Building2 className="h-4 w-4" aria-hidden="true" />
            Staff Console
          </Link>
        </header>

        <section className="grid flex-1 items-center gap-6 py-8 lg:grid-cols-[minmax(0,0.85fr)_minmax(0,1.15fr)]">
          <div className="animate-fade-up">
            <p className="text-sm font-semibold uppercase tracking-[0.16em] text-civic-primary">CivicSignal</p>
            <h1 className="mt-3 max-w-3xl text-4xl font-semibold tracking-normal text-civic-heading md:text-5xl">
              One platform for residents and city operations.
            </h1>
            <p className="mt-4 max-w-2xl text-base leading-7 text-civic-muted">
              Residents report issues through the public portal. City teams triage, review, route, and analyze those reports in the admin console.
            </p>
            <div className="mt-6 flex flex-col gap-3 sm:flex-row">
              <Link
                className="inline-flex h-12 items-center justify-center gap-2 rounded-md bg-civic-primary px-5 text-base font-semibold text-white hover:bg-civic-primary-strong"
                href="/public/report"
              >
                <ClipboardList className="h-5 w-5" aria-hidden="true" />
                Report Issue
              </Link>
              <Link
                className="inline-flex h-12 items-center justify-center gap-2 rounded-md border border-civic-border px-5 text-base font-semibold text-civic-primary hover:bg-civic-soft"
                href="/public/status"
              >
                <SearchCheck className="h-5 w-5" aria-hidden="true" />
                Track Status
              </Link>
            </div>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <PortalCard
              description="Live citizen feed, report status, and resident actions."
              href="/public/incidents"
              icon={<ListChecks className="h-5 w-5" />}
              label="Public Portal"
              stats="Feed first"
            />
            <PortalCard
              description="Staff dashboards, review queues, analytics, and settings."
              href="/admin/dashboard"
              icon={<Building2 className="h-5 w-5" />}
              label="Admin Console"
              stats="8 operations pages"
            />
            <PortalCard
              description="Inspect tokenization, embeddings, logits, and softmax routing."
              href="/public/model-lab"
              icon={<BrainCircuit className="h-5 w-5" />}
              label="Model Lab"
              stats="AI internals"
            />
            <Panel className="md:col-span-2" title="Architecture Boundary" description="The frontend speaks to the .NET API only. Database, AI models, queues, storage, and realtime services stay behind backend abstractions.">
              <div className="grid gap-3 md:grid-cols-3">
                {[
                  { label: "Clean API", icon: ShieldCheck },
                  { label: "AI-ready", icon: Sparkles },
                  { label: "Analytics-ready", icon: BarChart3 },
                ].map((item) => (
                  <div className="rounded-md border border-civic-border bg-civic-raised p-4" key={item.label}>
                    <item.icon className="h-5 w-5 text-civic-primary" aria-hidden="true" />
                    <div className="mt-3 text-sm font-semibold text-civic-heading">{item.label}</div>
                  </div>
                ))}
              </div>
            </Panel>
          </div>
        </section>
      </div>
    </main>
  );
}

function PortalCard({
  description,
  href,
  icon,
  label,
  stats,
}: {
  description: string;
  href: string;
  icon: ReactNode;
  label: string;
  stats: string;
}) {
  return (
    <Link
      className="animate-fade-up rounded-lg border border-civic-border bg-civic-surface p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-civic-primary hover:shadow-md"
      href={href}
    >
      <span className="inline-flex rounded-md bg-civic-soft p-2 text-civic-primary">{icon}</span>
      <h2 className="mt-4 text-2xl font-semibold text-civic-heading">{label}</h2>
      <p className="mt-2 text-sm leading-6 text-civic-muted">{description}</p>
      <p className="mt-5 rounded-md bg-civic-raised px-3 py-2 text-sm font-semibold text-civic-primary">{stats}</p>
    </Link>
  );
}
