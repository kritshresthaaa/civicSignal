"use client";

import { useState } from "react";
import { KeyRound, Loader2, ShieldCheck, UserCheck } from "lucide-react";
import type { AdminSessionState } from "@/lib/admin-auth";
import { fieldClassName } from "@/components/ui-kit";

type AdminLoginPanelProps = {
  message: string;
  onSignIn: (email: string, password: string) => Promise<unknown>;
  state: AdminSessionState;
};

export function AdminLoginPanel({ message, onSignIn, state }: AdminLoginPanelProps) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  return (
    <main className="min-h-screen bg-civic-page px-4 py-10 text-civic-ink sm:px-6 lg:px-8">
      <div className="mx-auto grid max-w-5xl gap-6 lg:grid-cols-[0.9fr_1.1fr]">
        <section className="rounded-lg border border-civic-border bg-civic-surface p-6 shadow-sm">
          <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-civic-primary text-white">
            <ShieldCheck className="h-6 w-6" aria-hidden="true" />
          </div>
          <h1 className="mt-5 text-3xl font-semibold tracking-normal text-civic-heading">CivicSignal Staff Access</h1>
          <p className="mt-3 text-sm leading-6 text-civic-muted">
            Admin, operator, and reviewer workspaces are protected. Public reporting remains available without signing in.
          </p>
          <div className="mt-6 grid gap-3">
            {[
              "Administrators and operators can manage operations, imports, and live streams.",
              "Reviewers can inspect AI evidence and submit human decisions.",
              "Residents use tracking codes instead of internal incident IDs.",
            ].map((item) => (
              <div className="flex items-start gap-3 rounded-md border border-civic-border bg-civic-raised p-3 text-sm text-civic-muted" key={item}>
                <UserCheck className="mt-0.5 h-4 w-4 shrink-0 text-civic-primary" aria-hidden="true" />
                <span>{item}</span>
              </div>
            ))}
          </div>
        </section>

        <section className="rounded-lg border border-civic-border bg-civic-surface p-6 shadow-sm">
          <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
            <KeyRound className="h-4 w-4 text-civic-primary" aria-hidden="true" />
            Sign In
          </div>
          <p className="mt-2 text-sm leading-6 text-civic-muted">{message}</p>

          <div className="mt-5 grid gap-4">
            <label className="block">
              <span className="mb-2 block text-sm font-semibold text-civic-heading">Email</span>
              <input
                autoComplete="email"
                className={fieldClassName}
                onChange={(event) => setEmail(event.target.value)}
                placeholder="operator@civicsignal.local"
                value={email}
              />
            </label>
            <label className="block">
              <span className="mb-2 block text-sm font-semibold text-civic-heading">Password</span>
              <input
                autoComplete="current-password"
                className={fieldClassName}
                onChange={(event) => setPassword(event.target.value)}
                placeholder="Password"
                type="password"
                value={password}
              />
            </label>
            <button
              className="inline-flex h-11 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-sm font-semibold text-white transition hover:bg-civic-primary-strong disabled:cursor-not-allowed disabled:opacity-60"
              disabled={state === "loading" || !email.trim() || !password}
              onClick={() => void onSignIn(email.trim(), password)}
              type="button"
            >
              {state === "loading" ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <KeyRound className="h-4 w-4" aria-hidden="true" />}
              Continue
            </button>
          </div>
        </section>
      </div>
    </main>
  );
}
