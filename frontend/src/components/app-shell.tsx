"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import type { ReactNode } from "react";
import { useEffect, useMemo, useState } from "react";
import {
  Activity,
  BarChart3,
  Bell,
  Bot,
  BrainCircuit,
  ClipboardCheck,
  Command,
  Database,
  FilePlus2,
  LayoutDashboard,
  Loader2,
  LockKeyhole,
  LogOut,
  Menu,
  RadioTower,
  Search,
  SearchCheck,
  Settings,
  ShieldCheck,
  Sparkles,
  UserCheck,
  Wifi,
  X,
} from "lucide-react";
import { AdminLoginPanel } from "@/components/admin-login-panel";
import {
  allowedRolesForAdminPath,
  canAccessAdminPath,
  hasAnyRole,
  type AdminRole,
  useAdminSession,
} from "@/lib/admin-auth";
import { apiBaseUrl, getSystemIntegrations, type SystemIntegrationStatusDto } from "@/lib/civic-api";

const navItems = [
  { href: "/admin/dashboard", label: "Dashboard", icon: LayoutDashboard, roles: ["Administrator", "Operator", "Reviewer"] },
  { href: "/admin/report", label: "Field Intake", icon: FilePlus2, roles: ["Administrator", "Operator", "Reviewer"] },
  { href: "/admin/incidents", label: "Incidents", icon: SearchCheck, roles: ["Administrator", "Operator", "Reviewer"] },
  { href: "/admin/review", label: "Review", icon: ClipboardCheck, roles: ["Administrator", "Operator", "Reviewer"] },
  { href: "/admin/data-sources", label: "Data Sources", icon: Database, roles: ["Administrator", "Operator"] },
  { href: "/admin/analytics", label: "Analytics", icon: BarChart3, roles: ["Administrator", "Operator", "Reviewer"] },
  { href: "/admin/ai-evaluation", label: "AI Evaluation", icon: BrainCircuit, roles: ["Administrator", "Operator", "Reviewer"] },
  { href: "/admin/model-lab", label: "Model Lab", icon: Sparkles, roles: ["Administrator", "Operator", "Reviewer"] },
  { href: "/admin/settings", label: "Settings", icon: Settings, roles: ["Administrator", "Operator", "Reviewer"] },
] satisfies Array<{ href: string; icon: typeof LayoutDashboard; label: string; roles: AdminRole[] }>;

export function AdminShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const adminSession = useAdminSession();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [commandOpen, setCommandOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [integrationPreview, setIntegrationPreview] = useState<SystemIntegrationStatusDto[]>([]);

  const allowedNavItems = useMemo(() => {
    return adminSession.session
      ? navItems.filter((item) => hasAnyRole(adminSession.session!.user, item.roles))
      : [];
  }, [adminSession.session]);

  const commandResults = useMemo(() => {
    const term = query.trim().toLowerCase();

    return allowedNavItems.filter((item) => !term || item.label.toLowerCase().includes(term) || item.href.includes(term));
  }, [allowedNavItems, query]);

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      const target = event.target as HTMLElement | null;
      const typing = target?.tagName === "INPUT" || target?.tagName === "TEXTAREA" || target?.isContentEditable;

      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setCommandOpen((current) => !current);
      }

      if (event.key === "/" && !typing) {
        event.preventDefault();
        setCommandOpen(true);
      }

      if (event.key === "Escape") {
        setCommandOpen(false);
      }
    }

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  useEffect(() => {
    if (!adminSession.session) {
      const timer = window.setTimeout(() => {
        setIntegrationPreview([]);
      }, 0);

      return () => window.clearTimeout(timer);
    }

    let ignore = false;

    getSystemIntegrations()
      .then((result) => {
        if (!ignore) {
          setIntegrationPreview(result.integrations);
        }
      })
      .catch(() => {
        if (!ignore) {
          setIntegrationPreview([]);
        }
      });

    return () => {
      ignore = true;
    };
  }, [adminSession.session]);

  function navigateTo(href: string) {
    router.push(href);
    setCommandOpen(false);
    setMobileOpen(false);
    setQuery("");
  }

  if (adminSession.state === "loading") {
    return (
      <main className="flex min-h-screen items-center justify-center bg-civic-page px-4 text-civic-ink">
        <div className="rounded-lg border border-civic-border bg-civic-surface p-6 text-center shadow-sm">
          <Loader2 className="mx-auto h-6 w-6 animate-spin text-civic-primary" aria-hidden="true" />
          <p className="mt-3 text-sm font-semibold text-civic-muted">Checking staff session...</p>
        </div>
      </main>
    );
  }

  if (!adminSession.session) {
    return <AdminLoginPanel message={adminSession.message} onSignIn={adminSession.signIn} state={adminSession.state} />;
  }

  const activeUser = adminSession.session.user;
  const routeAllowed = canAccessAdminPath(pathname, activeUser);
  const visibleChildren = routeAllowed ? children : <AccessRestricted pathname={pathname} roles={allowedRolesForAdminPath(pathname)} />;
  const backendRouteLabel = apiBaseUrl || "Same-origin proxy";

  return (
    <div className="min-h-screen bg-civic-page text-civic-ink">
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-72 border-r border-civic-border bg-civic-surface lg:flex lg:flex-col">
        <div className="border-b border-civic-border p-5">
          <Link className="flex items-center gap-3" href="/admin/dashboard">
            <span className="scan-line flex h-11 w-11 items-center justify-center rounded-lg bg-civic-primary text-white">
              <Sparkles className="h-5 w-5" aria-hidden="true" />
            </span>
            <span>
              <span className="block text-lg font-semibold text-civic-heading">CivicSignal AI</span>
              <span className="block text-sm text-civic-muted">City operations console</span>
            </span>
          </Link>
        </div>

        <nav className="flex-1 space-y-1 px-3 py-4">
          {allowedNavItems.map((item) => (
            <NavLink active={isActive(pathname, item.href)} href={item.href} key={item.href} label={item.label}>
              <item.icon className="h-5 w-5" aria-hidden="true" />
            </NavLink>
          ))}
        </nav>

        <div className="border-t border-civic-border p-4">
          <div className="rounded-lg border border-civic-border bg-civic-raised p-4 shadow-sm">
            <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
              <ShieldCheck className="h-4 w-4 text-civic-primary" aria-hidden="true" />
              Staff session
            </div>
            <p className="mt-2 break-all text-sm leading-6 text-civic-muted">{activeUser.displayName || activeUser.email}</p>
            <p className="text-xs font-semibold text-civic-primary">{activeUser.roles.join(" / ")}</p>
            <p className="mt-2 break-all text-xs text-civic-muted">{backendRouteLabel}</p>
            <div className="mt-4 grid gap-2">
              {buildSidebarIntegrationRows(integrationPreview).map((item) => (
                <div className="flex items-center justify-between gap-3 text-xs font-semibold text-civic-muted" key={item.name}>
                  <span className="truncate">{item.name}</span>
                  <span className={`flex shrink-0 items-center gap-1 ${item.enabled ? "text-civic-primary" : "text-civic-muted"}`}>
                    <span className={`h-2 w-2 rounded-full ${item.enabled ? "status-dot bg-civic-primary" : "bg-civic-border-strong"}`} />
                    {item.status}
                  </span>
                </div>
              ))}
            </div>
            <button
              className="mt-4 inline-flex h-9 w-full items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface px-3 text-sm font-semibold text-civic-muted transition hover:bg-white hover:text-civic-primary"
              onClick={adminSession.signOut}
              type="button"
            >
              <LogOut className="h-4 w-4" aria-hidden="true" />
              Sign out
            </button>
          </div>
        </div>
      </aside>

      <header className="sticky top-0 z-20 border-b border-civic-border bg-civic-surface/95 backdrop-blur lg:hidden">
        <div className="flex items-center justify-between px-4 py-3">
          <Link className="flex items-center gap-2 text-base font-semibold text-civic-heading" href="/admin/dashboard">
            <span className="flex h-9 w-9 items-center justify-center rounded-md bg-civic-primary text-white">
              <Sparkles className="h-4 w-4" aria-hidden="true" />
            </span>
            CivicSignal AI
          </Link>
          <button
            aria-label={mobileOpen ? "Close navigation" : "Open navigation"}
            className="inline-flex h-10 w-10 items-center justify-center rounded-md border border-civic-border text-civic-primary"
            onClick={() => setMobileOpen((current) => !current)}
            type="button"
          >
            {mobileOpen ? <X className="h-5 w-5" aria-hidden="true" /> : <Menu className="h-5 w-5" aria-hidden="true" />}
          </button>
        </div>
        {mobileOpen ? (
          <nav className="animate-slide-down grid gap-1 border-t border-civic-border px-3 py-3">
            {allowedNavItems.map((item) => (
              <NavLink
                active={isActive(pathname, item.href)}
                href={item.href}
                key={item.href}
                label={item.label}
                onNavigate={() => setMobileOpen(false)}
              >
                <item.icon className="h-5 w-5" aria-hidden="true" />
              </NavLink>
            ))}
          </nav>
        ) : null}
      </header>

      <main className="lg:pl-72">
        <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
          <div className="mb-6 hidden items-center justify-between rounded-lg border border-civic-border bg-civic-surface px-4 py-3 shadow-sm lg:flex">
            <div className="flex items-center gap-4 text-sm text-civic-muted">
              <span className="flex items-center gap-2">
                <Bell className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                Live operations
              </span>
              <span className="hidden items-center gap-2 xl:flex">
                <Wifi className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                Streaming-ready
              </span>
              <span className="hidden items-center gap-2 xl:flex">
                <Bot className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                AI-assisted triage
              </span>
              <span className="hidden items-center gap-2 xl:flex">
                <UserCheck className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                {activeUser.roles[0] ?? "Staff"}
              </span>
            </div>
            <button
              className="inline-flex h-10 min-w-80 items-center justify-between rounded-md border border-civic-border bg-civic-raised px-3 text-sm text-civic-muted transition hover:border-civic-border-strong hover:bg-civic-soft"
              onClick={() => setCommandOpen(true)}
              type="button"
            >
              <span className="flex items-center gap-2">
                <Search className="h-4 w-4" aria-hidden="true" />
                Search pages and workflows
              </span>
              <span className="rounded border border-civic-border bg-civic-surface px-2 py-0.5 text-xs font-semibold">Cmd K</span>
            </button>
          </div>
          <div className="animate-fade-up">{visibleChildren}</div>
        </div>
      </main>

      {commandOpen ? (
        <div className="fixed inset-0 z-50 flex items-start justify-center bg-civic-heading/30 px-4 py-20 backdrop-blur-sm">
          <div className="animate-scale-in w-full max-w-2xl rounded-lg border border-civic-border bg-civic-surface shadow-2xl">
            <div className="flex items-center gap-3 border-b border-civic-border px-4 py-3">
              <Command className="h-5 w-5 text-civic-primary" aria-hidden="true" />
              <input
                autoFocus
                className="h-11 flex-1 bg-transparent text-base outline-none placeholder:text-civic-muted"
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Jump to dashboard, review, analytics, AI evaluation..."
                value={query}
              />
              <button
                aria-label="Close command center"
                className="inline-flex h-9 w-9 items-center justify-center rounded-md text-civic-muted hover:bg-civic-soft hover:text-civic-primary"
                onClick={() => setCommandOpen(false)}
                type="button"
              >
                <X className="h-5 w-5" aria-hidden="true" />
              </button>
            </div>
            <div className="max-h-[420px] overflow-y-auto p-3">
              <div className="grid gap-2">
                {commandResults.map((item) => (
                  <button
                    className="group flex items-center justify-between rounded-md border border-civic-border bg-civic-raised p-3 text-left transition hover:border-civic-primary hover:bg-civic-soft"
                    key={item.href}
                    onClick={() => navigateTo(item.href)}
                    type="button"
                  >
                    <span className="flex items-center gap-3">
                      <span className="rounded-md bg-civic-surface p-2 text-civic-primary group-hover:bg-white">
                        <item.icon className="h-4 w-4" aria-hidden="true" />
                      </span>
                      <span>
                        <span className="block font-semibold text-civic-heading">{item.label}</span>
                        <span className="text-sm text-civic-muted">{item.href}</span>
                      </span>
                    </span>
                    <RadioTower className="h-4 w-4 text-civic-muted" aria-hidden="true" />
                  </button>
                ))}
              </div>
              <div className="mt-3 grid gap-2 border-t border-civic-border pt-3 sm:grid-cols-2">
                <button
                  className="rounded-md border border-civic-border bg-civic-raised p-3 text-left transition hover:bg-civic-soft"
                  onClick={() => navigateTo("/public/report")}
                  type="button"
                >
                  <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
                    <FilePlus2 className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                    New incident
                  </div>
                  <p className="mt-1 text-sm text-civic-muted">Open citizen intake</p>
                </button>
                <button
                  className="rounded-md border border-civic-border bg-civic-raised p-3 text-left transition hover:bg-civic-soft"
                  onClick={() => navigateTo("/admin/review")}
                  type="button"
                >
                  <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
                    <Activity className="h-4 w-4 text-civic-primary" aria-hidden="true" />
                    Review queue
                  </div>
                  <p className="mt-1 text-sm text-civic-muted">Open human review</p>
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function AccessRestricted({ pathname, roles }: { pathname: string; roles: AdminRole[] }) {
  return (
    <div className="rounded-lg border border-status-review bg-status-review/20 p-6 text-civic-ink shadow-sm">
      <div className="flex items-center gap-3">
        <span className="flex h-11 w-11 items-center justify-center rounded-lg bg-civic-surface text-status-review-text">
          <LockKeyhole className="h-5 w-5" aria-hidden="true" />
        </span>
        <div>
          <h1 className="text-xl font-semibold text-civic-heading">Access restricted</h1>
          <p className="mt-1 text-sm text-civic-muted">{pathname} requires {roles.join(", ")} access.</p>
        </div>
      </div>
    </div>
  );
}

function NavLink({
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
      className={`group relative flex h-11 items-center gap-3 overflow-hidden rounded-md px-3 text-sm font-semibold transition ${
        active ? "bg-civic-primary text-white shadow-sm" : "text-civic-muted hover:bg-civic-soft hover:text-civic-primary"
      }`}
      href={href}
      onClick={onNavigate}
    >
      {active ? <span className="absolute left-0 top-2 h-7 w-1 rounded-r bg-white/90" /> : null}
      {children}
      {label}
    </Link>
  );
}

function buildSidebarIntegrationRows(integrations: SystemIntegrationStatusDto[]) {
  if (!integrations.length) {
    return [
      { enabled: false, name: "Backend status", status: "Loading" },
      { enabled: false, name: "AI route", status: "Loading" },
      { enabled: false, name: "Queue", status: "Loading" },
    ];
  }

  const preferred = ["PostgreSQL/PostGIS", "Python AI service", "RabbitMQ worker queue"];

  return preferred.map((name) => {
    const match = integrations.find((item) => item.name === name);

    return {
      enabled: match?.enabled ?? false,
      name: name.replace(" worker queue", ""),
      status: match?.status ?? "Unknown",
    };
  });
}

function isActive(pathname: string, href: string) {
  return pathname === href || pathname.startsWith(`${href}/`);
}
