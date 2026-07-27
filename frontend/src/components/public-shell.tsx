"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { useState } from "react";
import { BrainCircuit, Building2, ClipboardList, HelpCircle, ListChecks, Menu, SearchCheck, Sparkles, X } from "lucide-react";
import { PwaStatus } from "@/components/pwa-status";

const publicNav = [
  { href: "/public/incidents", label: "Feed", icon: ListChecks },
  { href: "/public/report", label: "Report", icon: ClipboardList },
  { href: "/public/status", label: "Status", icon: SearchCheck },
  { href: "/public/model-lab", label: "Model Lab", icon: BrainCircuit },
  { href: "/public/help", label: "Help", icon: HelpCircle },
];

export function PublicShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const [open, setOpen] = useState(false);

  return (
    <div className="min-h-screen bg-civic-page text-civic-ink">
      <header className="sticky top-0 z-30 border-b border-civic-border bg-civic-surface/95 backdrop-blur">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-4 sm:px-6 lg:px-8">
          <Link className="flex items-center gap-3" href="/public/incidents">
            <span className="scan-line flex h-11 w-11 items-center justify-center rounded-lg bg-civic-primary text-white">
              <Sparkles className="h-5 w-5" aria-hidden="true" />
            </span>
            <span>
              <span className="block text-lg font-semibold text-civic-heading">CivicSignal</span>
              <span className="hidden text-sm text-civic-muted sm:block">Resident portal</span>
            </span>
          </Link>

          <nav className="hidden items-center gap-2 md:flex">
            {publicNav.map((item) => (
              <PublicNavLink active={isActive(pathname, item.href)} href={item.href} key={item.href} label={item.label}>
                <item.icon className="h-4 w-4" aria-hidden="true" />
              </PublicNavLink>
            ))}
            <Link
              className="ml-2 inline-flex h-10 items-center gap-2 rounded-md border border-civic-border px-3 text-sm font-semibold text-civic-primary transition hover:bg-civic-soft"
              href="/admin/dashboard"
            >
              <Building2 className="h-4 w-4" aria-hidden="true" />
              Staff
            </Link>
          </nav>

          <button
            aria-label={open ? "Close navigation" : "Open navigation"}
            className="inline-flex h-10 w-10 items-center justify-center rounded-md border border-civic-border text-civic-primary md:hidden"
            onClick={() => setOpen((current) => !current)}
            type="button"
          >
            {open ? <X className="h-5 w-5" aria-hidden="true" /> : <Menu className="h-5 w-5" aria-hidden="true" />}
          </button>
        </div>

        {open ? (
          <nav className="animate-slide-down grid gap-1 border-t border-civic-border px-4 py-3 md:hidden">
            {publicNav.map((item) => (
              <PublicNavLink
                active={isActive(pathname, item.href)}
                href={item.href}
                key={item.href}
                label={item.label}
                onNavigate={() => setOpen(false)}
              >
                <item.icon className="h-4 w-4" aria-hidden="true" />
              </PublicNavLink>
            ))}
            <Link
              className="flex h-11 items-center gap-2 rounded-md border border-civic-border px-3 text-sm font-semibold text-civic-primary"
              href="/admin/dashboard"
              onClick={() => setOpen(false)}
            >
              <Building2 className="h-4 w-4" aria-hidden="true" />
              Staff
            </Link>
          </nav>
        ) : null}
      </header>
      <PwaStatus />

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
        <div className="animate-fade-up">{children}</div>
      </main>
    </div>
  );
}

function PublicNavLink({
  active,
  children,
  href,
  label,
  onNavigate,
}: {
  active: boolean;
  children: ReactNode;
  href: string;
  label: string;
  onNavigate?: () => void;
}) {
  return (
    <Link
      className={`inline-flex h-10 items-center gap-2 rounded-md px-3 text-sm font-semibold transition ${
        active ? "bg-civic-primary text-white shadow-sm" : "text-civic-muted hover:bg-civic-soft hover:text-civic-primary"
      }`}
      href={href}
      onClick={onNavigate}
    >
      {children}
      {label}
    </Link>
  );
}

function isActive(pathname: string, href: string) {
  if (href === "/public/incidents") {
    return pathname === "/public" || pathname === "/public/incidents";
  }

  return pathname === href || pathname.startsWith(`${href}/`);
}
