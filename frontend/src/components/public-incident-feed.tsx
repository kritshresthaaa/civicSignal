"use client";

import Link from "next/link";
import {
  type FilterSpecification,
  type GeoJSONSource,
  Map as MapLibre,
  type MapLayerMouseEvent,
  type MapLibreMap,
  NavigationControl,
  ScaleControl,
  type StyleSpecification,
} from "maplibre-gl";
import type { ReactNode } from "react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Activity,
  AlertTriangle,
  ArrowRight,
  Bell,
  Camera,
  CheckCircle2,
  CircleUserRound,
  Clock,
  Copy,
  Eye,
  FileImage,
  Filter,
  Heart,
  ImageIcon,
  Layers,
  Map as MapIcon,
  MapPin,
  Megaphone,
  MessageCircle,
  MessageSquareWarning,
  MoreHorizontal,
  Navigation,
  RefreshCw,
  Route,
  Search,
  Send,
  Share2,
  ShieldCheck,
  Sparkles,
  ThumbsUp,
  TrendingUp,
  Wifi,
  WifiOff,
} from "lucide-react";
import { statusLabel } from "@/lib/civic-analysis";
import {
  CivicApiError,
  searchPublicIncidents,
  type PublicIncidentFeedItemDto,
} from "@/lib/civic-api";
import type { IncidentStatus, Severity } from "@/lib/civic-types";
import { fieldClassName, Panel, SeverityBadge, StatusBadge } from "@/components/ui-kit";

const statusFilters = ["All", "Submitted", "Triaged", "HumanReviewRequired", "Approved", "Dispatched"] as const;
type StatusFilter = (typeof statusFilters)[number];
const feedViews = ["All", "Photos", "NeedsAction", "Duplicates"] as const;
type FeedView = (typeof feedViews)[number];

const incidentStatuses: readonly IncidentStatus[] = ["Submitted", "Triaged", "HumanReviewRequired", "Approved", "Dispatched"];
const severities: readonly Severity[] = ["Low", "Medium", "High", "Critical"];
const feedMapSourceId = "public-feed-incidents";
const feedMapLayerIds = {
  heat: "public-feed-incidents-heat",
  marker: "public-feed-incidents-marker",
  selectedHalo: "public-feed-incidents-selected-halo",
  selectedRing: "public-feed-incidents-selected-ring",
} as const;
const defaultFeedMapCenter = { latitude: 40.7128, longitude: -74.0060 };

type FeedMapFeatureProperties = {
  areaLabel: string;
  category: string;
  description: string;
  severity: string;
  status: string;
  trackingCode: string;
};

export function PublicIncidentFeed() {
  const [items, setItems] = useState<PublicIncidentFeedItemDto[]>([]);
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("All");
  const [feedView, setFeedView] = useState<FeedView>("All");
  const [query, setQuery] = useState("");
  const [selectedTrackingCode, setSelectedTrackingCode] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

  const loadFeed = useCallback(
    async ({ silent = false }: { silent?: boolean } = {}) => {
      if (!silent) {
        setLoading(true);
      }

      setRefreshing(true);

      try {
        const results = await searchPublicIncidents({
          pageSize: 50,
          status: statusFilter === "All" ? undefined : statusFilter,
        });

        setItems(results);
        setLastUpdated(new Date());
        setError(null);
      } catch (loadError) {
        setError(loadError instanceof CivicApiError ? loadError.message : "Could not load the public incident feed.");
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [statusFilter],
  );

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void loadFeed();
    }, 0);

    return () => window.clearTimeout(timeoutId);
  }, [loadFeed]);

  useEffect(() => {
    if (!autoRefresh) {
      return;
    }

    const intervalId = window.setInterval(() => {
      void loadFeed({ silent: true });
    }, 30_000);

    return () => window.clearInterval(intervalId);
  }, [autoRefresh, loadFeed]);

  const filteredItems = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase();

    return items.filter((item) => {
      const matchesView = matchesFeedView(item, feedView);
      if (!matchesView) {
        return false;
      }

      if (!normalizedQuery) {
        return true;
      }

      return [
        item.trackingCode,
        item.description,
        item.category,
        formatCategory(item.category),
        item.severity,
        item.status,
        item.agencyCode ?? "",
        item.areaLabel,
      ]
        .join(" ")
        .toLowerCase()
        .includes(normalizedQuery);
    });
  }, [feedView, items, query]);

  const selectedItem = filteredItems.find((item) => item.trackingCode === selectedTrackingCode) ?? filteredItems[0] ?? null;
  const summary = useMemo(() => buildSummary(items), [items]);
  const categoryCounts = useMemo(() => buildCategoryCounts(filteredItems), [filteredItems]);
  const globalCategoryCounts = useMemo(() => buildCategoryCounts(items), [items]);

  return (
    <div className="space-y-6">
      <PublicFeedHero
        autoRefresh={autoRefresh}
        categoryCounts={globalCategoryCounts}
        feedView={feedView}
        items={items}
        lastUpdated={lastUpdated}
        onCategorySelect={setQuery}
        onRefresh={() => void loadFeed()}
        onStatusChange={setStatusFilter}
        onToggleLive={() => setAutoRefresh((current) => !current)}
        onViewChange={setFeedView}
        refreshing={refreshing}
        statusFilter={statusFilter}
        summary={summary}
      />

      <FeedComposer />
      <FeedStories items={items} statusFilter={statusFilter} onChange={setStatusFilter} />

      {error ? (
        <Panel className="border-status-critical/40" title="Feed Unavailable">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <p className="text-sm text-civic-muted">{error}</p>
            <button
              className="inline-flex h-10 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-sm font-semibold text-white transition hover:bg-civic-primary-strong"
              onClick={() => void loadFeed()}
              type="button"
            >
              <RefreshCw className="h-4 w-4" aria-hidden="true" />
              Try Again
            </button>
          </div>
        </Panel>
      ) : null}

      <div className="grid gap-6 xl:grid-cols-[280px_minmax(0,1fr)_380px]">
        <aside className="hidden space-y-4 xl:block xl:sticky xl:top-28 xl:self-start">
          <Panel title="Feed Filters" description={lastUpdated ? `Updated ${lastUpdated.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}` : "Loading from the public API"}>
            <div className="space-y-3">
              {statusFilters.map((option) => (
                <button
                  aria-pressed={statusFilter === option}
                  className={`flex w-full items-center justify-between rounded-md border px-3 py-3 text-left text-sm font-semibold transition ${
                    statusFilter === option
                      ? "border-civic-primary bg-civic-soft text-civic-primary"
                      : "border-civic-border bg-civic-raised text-civic-muted hover:border-civic-border-strong hover:bg-civic-soft"
                  }`}
                  key={option}
                  onClick={() => setStatusFilter(option)}
                  type="button"
                >
                  <span>{option === "All" ? "All reports" : statusLabel(option)}</span>
                  <span className="rounded-md bg-civic-surface px-2 py-1 text-xs">{countStatus(items, option)}</span>
                </button>
              ))}
            </div>
          </Panel>

          <Panel title="Community Snapshot">
            <div className="grid gap-3">
              <PublicMetric icon={<Sparkles className="h-5 w-5" />} label="Visible reports" value={String(items.length)} />
              <PublicMetric icon={<Clock className="h-5 w-5" />} label="Open reports" value={String(summary.open)} tone="review" />
              <PublicMetric icon={<ShieldCheck className="h-5 w-5" />} label="Reviewed" value={String(summary.reviewed)} tone="calm" />
              <PublicMetric icon={<AlertTriangle className="h-5 w-5" />} label="Duplicates" value={String(summary.duplicates)} tone="alert" />
            </div>
          </Panel>
        </aside>

        <section className="space-y-4" aria-live="polite">
          <FeedToolbar
            autoRefresh={autoRefresh}
            lastUpdated={lastUpdated}
            onRefresh={() => void loadFeed()}
            onSearch={setQuery}
            onToggleLive={() => setAutoRefresh((current) => !current)}
            query={query}
            refreshing={refreshing}
            statusFilter={statusFilter}
            onStatusChange={setStatusFilter}
          />
          {loading ? <FeedSkeleton /> : null}
          {!loading && filteredItems.length === 0 ? <EmptyFeed query={query} /> : null}
          {!loading
            ? filteredItems.map((item) => (
                <FeedItem
                  item={item}
                  key={item.trackingCode}
                  onSelect={() => setSelectedTrackingCode(item.trackingCode)}
                  selected={item.trackingCode === selectedItem?.trackingCode}
                />
              ))
            : null}
        </section>

        <aside className="space-y-4 xl:sticky xl:top-28 xl:self-start">
          <CommunityMapPreview
            items={filteredItems}
            onSelect={setSelectedTrackingCode}
            selectedTrackingCode={selectedItem?.trackingCode ?? null}
          />
          <SelectedReport item={selectedItem} />
          <Panel title="Area Pulse" description={`${filteredItems.length} matching public reports`}>
            <div className="space-y-3">
              {categoryCounts.length ? (
                categoryCounts.map(([category, count]) => (
                  <div className="rounded-md border border-civic-border bg-civic-raised p-3" key={category}>
                    <div className="flex items-center justify-between gap-3 text-sm">
                      <span className="font-semibold text-civic-heading">{formatCategory(category)}</span>
                      <span className="text-civic-muted">{count} reports</span>
                    </div>
                    <div className="mt-3 h-2 rounded-full bg-civic-border">
                      <div
                        className="h-2 rounded-full bg-civic-primary transition-all duration-500"
                        style={{ width: `${Math.max(8, Math.round((count / Math.max(1, filteredItems.length)) * 100))}%` }}
                      />
                    </div>
                  </div>
                ))
              ) : (
                <p className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm text-civic-muted">
                  No public reports match the current view.
                </p>
              )}
            </div>
          </Panel>
        </aside>
      </div>
    </div>
  );
}

function PublicFeedHero({
  autoRefresh,
  categoryCounts,
  feedView,
  items,
  lastUpdated,
  onCategorySelect,
  onRefresh,
  onStatusChange,
  onToggleLive,
  onViewChange,
  refreshing,
  statusFilter,
  summary,
}: {
  autoRefresh: boolean;
  categoryCounts: Array<[string, number]>;
  feedView: FeedView;
  items: PublicIncidentFeedItemDto[];
  lastUpdated: Date | null;
  onCategorySelect: (category: string) => void;
  onRefresh: () => void;
  onStatusChange: (status: StatusFilter) => void;
  onToggleLive: () => void;
  onViewChange: (view: FeedView) => void;
  refreshing: boolean;
  statusFilter: StatusFilter;
  summary: ReturnType<typeof buildSummary>;
}) {
  const topCategory = categoryCounts[0]?.[0];
  const updatedLabel = lastUpdated
    ? lastUpdated.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })
    : "Waiting";

  return (
    <section className="relative overflow-hidden rounded-lg border border-civic-border bg-civic-surface shadow-sm">
      <div className="absolute inset-0 opacity-70 [background-image:linear-gradient(rgba(42,129,110,0.08)_1px,transparent_1px),linear-gradient(90deg,rgba(42,129,110,0.08)_1px,transparent_1px)] [background-size:42px_42px]" />
      <div className="absolute inset-x-0 bottom-0 h-24 border-t border-civic-border bg-civic-soft/50" />
      <div className="relative grid gap-6 p-5 lg:grid-cols-[1.1fr_0.9fr] lg:p-6">
        <div className="flex min-h-[330px] flex-col justify-between">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-civic-border bg-civic-surface px-3 py-1.5 text-xs font-semibold text-civic-primary shadow-sm">
              <Activity className="h-4 w-4" aria-hidden="true" />
              Resident signal
            </div>
            <h1 className="mt-5 max-w-3xl text-4xl font-semibold leading-tight text-civic-heading sm:text-5xl">
              Public Incident Feed
            </h1>
            <p className="mt-4 max-w-2xl text-base leading-7 text-civic-muted">
              Live community reports, public status, approximate locations, and AI-assisted routing signals.
            </p>
          </div>

          <div className="mt-6 grid gap-3 sm:grid-cols-2">
            <Link
              className="inline-flex h-12 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-sm font-semibold text-white shadow-sm transition hover:bg-civic-primary-strong"
              href="/public/report"
            >
              <MessageSquareWarning className="h-4 w-4" aria-hidden="true" />
              Report a Problem
            </Link>
            <Link
              className="inline-flex h-12 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface px-4 text-sm font-semibold text-civic-primary shadow-sm transition hover:bg-civic-soft"
              href="/public/status"
            >
              <Bell className="h-4 w-4" aria-hidden="true" />
              Track a Report
            </Link>
          </div>
        </div>

        <div className="rounded-lg border border-civic-border bg-civic-surface/95 p-4 shadow-sm backdrop-blur">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p className="text-sm font-semibold text-civic-heading">City Pulse</p>
              <p className="text-xs font-semibold text-civic-muted">
                {topCategory ? `${formatCategory(topCategory)} is leading the feed` : "Ready for the first report"}
              </p>
            </div>
            <div className="flex items-center gap-2">
              <button
                aria-pressed={autoRefresh}
                className={`inline-flex h-9 items-center gap-2 rounded-md border px-3 text-xs font-semibold transition ${
                  autoRefresh
                    ? "border-civic-primary bg-civic-soft text-civic-primary"
                    : "border-civic-border text-civic-muted hover:bg-civic-soft"
                }`}
                onClick={onToggleLive}
                type="button"
              >
                {autoRefresh ? <Wifi className="h-4 w-4" aria-hidden="true" /> : <WifiOff className="h-4 w-4" aria-hidden="true" />}
                {autoRefresh ? "Live" : "Paused"}
              </button>
              <button
                aria-label="Refresh public feed"
                className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-civic-border text-civic-primary transition hover:bg-civic-soft disabled:cursor-not-allowed disabled:opacity-60"
                disabled={refreshing}
                onClick={onRefresh}
                type="button"
              >
                <RefreshCw className={`h-4 w-4 ${refreshing ? "animate-spin" : ""}`} aria-hidden="true" />
              </button>
            </div>
          </div>

          <div className="mt-4 grid grid-cols-2 gap-3">
            <HeroStat icon={<Route className="h-4 w-4" aria-hidden="true" />} label="Open" value={summary.open} />
            <HeroStat icon={<FileImage className="h-4 w-4" aria-hidden="true" />} label="Media" value={summary.media} />
            <HeroStat icon={<TrendingUp className="h-4 w-4" aria-hidden="true" />} label="Needs action" value={summary.needsAction} />
            <HeroStat icon={<AlertTriangle className="h-4 w-4" aria-hidden="true" />} label="Duplicates" value={summary.duplicates} />
          </div>

          <div className="mt-4 grid gap-2 sm:grid-cols-2">
            {feedViews.map((view) => (
              <FeedViewButton
                active={feedView === view}
                count={countFeedView(items, view)}
                icon={feedViewIcon(view)}
                key={view}
                label={feedViewLabel(view)}
                onClick={() => onViewChange(view)}
              />
            ))}
          </div>

          <div className="mt-4 rounded-lg border border-civic-border bg-civic-raised p-3">
            <div className="flex items-center justify-between gap-3 text-xs font-semibold text-civic-muted">
              <span>Updated {updatedLabel}</span>
              <span>{statusFilter === "All" ? "All reports" : statusLabel(statusFilter)}</span>
            </div>
            <div className="mt-3 flex flex-wrap gap-2">
              {statusFilters.slice(0, 4).map((status) => (
                <button
                  aria-pressed={statusFilter === status}
                  className={`rounded-full border px-3 py-1.5 text-xs font-semibold transition ${
                    statusFilter === status
                      ? "border-civic-primary bg-civic-primary text-white"
                      : "border-civic-border bg-civic-surface text-civic-muted hover:bg-civic-soft hover:text-civic-primary"
                  }`}
                  key={status}
                  onClick={() => onStatusChange(status)}
                  type="button"
                >
                  {status === "All" ? "All" : statusLabel(status)}
                </button>
              ))}
            </div>
          </div>

          <div className="mt-4 flex gap-2 overflow-x-auto pb-1">
            {categoryCounts.length ? (
              categoryCounts.slice(0, 5).map(([category, count]) => (
                <button
                  className="group min-w-36 rounded-lg border border-civic-border bg-civic-raised p-3 text-left transition hover:-translate-y-0.5 hover:border-civic-primary hover:bg-civic-soft"
                  key={category}
                  onClick={() => onCategorySelect(formatCategory(category))}
                  type="button"
                >
                  <span className="block truncate text-sm font-semibold text-civic-heading">{formatCategory(category)}</span>
                  <span className="mt-1 block text-xs font-semibold text-civic-muted">{count} reports</span>
                </button>
              ))
            ) : (
              <div className="w-full rounded-lg border border-dashed border-civic-border bg-civic-raised p-4 text-sm font-semibold text-civic-muted">
                No public reports yet.
              </div>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}

function HeroStat({ icon, label, value }: { icon: ReactNode; label: string; value: number }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-raised p-3">
      <div className="flex items-center justify-between gap-2">
        <span className="text-xs font-semibold text-civic-muted">{label}</span>
        <span className="rounded-md bg-civic-soft p-1.5 text-civic-primary">{icon}</span>
      </div>
      <p className="mt-2 text-2xl font-semibold text-civic-heading">{value}</p>
    </div>
  );
}

function FeedViewButton({
  active,
  count,
  icon,
  label,
  onClick,
}: {
  active: boolean;
  count: number;
  icon: ReactNode;
  label: string;
  onClick: () => void;
}) {
  return (
    <button
      aria-pressed={active}
      className={`flex min-h-14 items-center justify-between gap-3 rounded-md border px-3 text-left transition ${
        active
          ? "border-civic-primary bg-civic-soft text-civic-primary"
          : "border-civic-border bg-civic-raised text-civic-muted hover:border-civic-border-strong hover:bg-civic-soft hover:text-civic-primary"
      }`}
      onClick={onClick}
      type="button"
    >
      <span className="inline-flex min-w-0 items-center gap-2 text-sm font-semibold">
        {icon}
        <span className="truncate">{label}</span>
      </span>
      <span className="rounded-md bg-civic-surface px-2 py-1 text-xs font-semibold">{count}</span>
    </button>
  );
}

function FeedComposer() {
  return (
    <section className="rounded-lg border border-civic-border bg-civic-surface p-4 shadow-sm">
      <div className="flex items-center gap-3">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-civic-soft text-civic-primary">
          <CircleUserRound className="h-6 w-6" aria-hidden="true" />
        </span>
        <Link
          className="flex min-h-12 flex-1 items-center rounded-full border border-civic-border bg-civic-raised px-4 text-left text-sm font-semibold text-civic-muted transition hover:border-civic-primary hover:bg-civic-soft hover:text-civic-primary sm:text-base"
          href="/public/report"
        >
          What needs attention in your area?
        </Link>
      </div>
      <div className="mt-4 grid grid-cols-3 gap-2 border-t border-civic-border pt-3">
        <ComposerAction href="/public/report" icon={<Camera className="h-4 w-4" aria-hidden="true" />} label="Photo" />
        <ComposerAction href="/public/report" icon={<MapPin className="h-4 w-4" aria-hidden="true" />} label="Location" />
        <ComposerAction href="/public/status" icon={<Bell className="h-4 w-4" aria-hidden="true" />} label="Track" />
      </div>
    </section>
  );
}

function ComposerAction({ href, icon, label }: { href: string; icon: ReactNode; label: string }) {
  return (
    <Link
      className="inline-flex h-11 items-center justify-center gap-2 rounded-md text-sm font-semibold text-civic-muted transition hover:bg-civic-soft hover:text-civic-primary"
      href={href}
    >
      {icon}
      {label}
    </Link>
  );
}

function FeedStories({
  items,
  onChange,
  statusFilter,
}: {
  items: PublicIncidentFeedItemDto[];
  onChange: (status: StatusFilter) => void;
  statusFilter: StatusFilter;
}) {
  const storyItems = statusFilters.map((status) => ({
    count: countStatus(items, status),
    icon: storyIcon(status),
    label: status === "All" ? "All" : statusLabel(status),
    status,
  }));

  return (
    <section className="flex gap-3 overflow-x-auto pb-1">
      {storyItems.map((story) => (
        <button
          aria-pressed={statusFilter === story.status}
          className={`group grid h-32 w-28 shrink-0 content-between rounded-lg border p-3 text-left shadow-sm transition hover:-translate-y-0.5 hover:shadow-md ${
            statusFilter === story.status
              ? "border-civic-primary bg-civic-soft"
              : "border-civic-border bg-civic-surface hover:border-civic-border-strong"
          }`}
          key={story.status}
          onClick={() => onChange(story.status)}
          type="button"
        >
          <span
            className={`flex h-10 w-10 items-center justify-center rounded-full ${
              statusFilter === story.status ? "bg-civic-primary text-white" : "bg-civic-soft text-civic-primary"
            }`}
          >
            {story.icon}
          </span>
          <span>
            <span className="block text-lg font-semibold text-civic-heading">{story.count}</span>
            <span className="block text-xs font-semibold text-civic-muted">{story.label}</span>
          </span>
        </button>
      ))}
    </section>
  );
}

function FeedToolbar({
  autoRefresh,
  lastUpdated,
  onRefresh,
  onSearch,
  onStatusChange,
  onToggleLive,
  query,
  refreshing,
  statusFilter,
}: {
  autoRefresh: boolean;
  lastUpdated: Date | null;
  onRefresh: () => void;
  onSearch: (value: string) => void;
  onStatusChange: (status: StatusFilter) => void;
  onToggleLive: () => void;
  query: string;
  refreshing: boolean;
  statusFilter: StatusFilter;
}) {
  return (
    <section className="rounded-lg border border-civic-border bg-civic-surface p-4 shadow-sm">
      <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <label className="relative block flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-civic-muted" aria-hidden="true" />
          <input
            className={`${fieldClassName} rounded-full pl-10`}
            onChange={(event) => onSearch(event.target.value)}
            placeholder="Search reports, areas, categories"
            value={query}
          />
        </label>
        <div className="flex flex-wrap items-center gap-2">
          <button
            aria-pressed={autoRefresh}
            className={`inline-flex h-10 items-center gap-2 rounded-md border px-3 text-sm font-semibold transition ${
              autoRefresh
                ? "border-civic-primary bg-civic-soft text-civic-primary"
                : "border-civic-border text-civic-muted hover:bg-civic-soft"
            }`}
            onClick={onToggleLive}
            type="button"
          >
            {autoRefresh ? <Wifi className="h-4 w-4" aria-hidden="true" /> : <WifiOff className="h-4 w-4" aria-hidden="true" />}
            Live
          </button>
          <button
            className="inline-flex h-10 items-center gap-2 rounded-md border border-civic-border px-3 text-sm font-semibold text-civic-primary transition hover:bg-civic-soft disabled:cursor-not-allowed disabled:opacity-60"
            disabled={refreshing}
            onClick={onRefresh}
            type="button"
          >
            <RefreshCw className={`h-4 w-4 ${refreshing ? "animate-spin" : ""}`} aria-hidden="true" />
            Refresh
          </button>
        </div>
      </div>
      <div className="mt-3 flex items-center justify-between gap-3 text-xs font-semibold text-civic-muted">
        <span>{lastUpdated ? `Updated ${lastUpdated.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}` : "Loading feed"}</span>
        <select
          className="rounded-md border border-civic-border bg-civic-raised px-3 py-2 text-xs font-semibold text-civic-muted outline-none focus:border-civic-primary"
          onChange={(event) => onStatusChange(event.target.value as StatusFilter)}
          value={statusFilter}
        >
          {statusFilters.map((status) => (
            <option key={status} value={status}>
              {status === "All" ? "All reports" : statusLabel(status)}
            </option>
          ))}
        </select>
      </div>
    </section>
  );
}

function FeedItem({
  item,
  onSelect,
  selected,
}: {
  item: PublicIncidentFeedItemDto;
  onSelect: () => void;
  selected: boolean;
}) {
  const [helpful, setHelpful] = useState(false);
  const [copied, setCopied] = useState(false);
  const [imageFailed, setImageFailed] = useState(false);
  const status = normalizeStatus(item.status);
  const severity = normalizeSeverity(item.severity);
  const nextStep = nextPublicStep(item);
  const imageUrl = imageFailed ? null : resolvePublicMediaUrl(item.latestImageUrl);
  const mediaCount = item.mediaCount ?? 0;

  async function copyReportLink() {
    const url = `${window.location.origin}/public/incidents/${encodeURIComponent(item.trackingCode)}`;
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1800);
    } catch {
      setCopied(false);
    }
  }

  return (
    <article
      aria-current={selected}
      className={`group rounded-lg border bg-civic-surface shadow-sm transition duration-200 hover:-translate-y-0.5 hover:border-civic-primary hover:shadow-md ${
        selected ? "border-civic-primary bg-civic-soft/60" : "border-civic-border"
      }`}
    >
      <header className="flex items-start gap-3 p-4 pb-3">
        <button
          aria-label={`Inspect ${item.trackingCode}`}
          className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-full text-base font-semibold text-white shadow-sm ${categoryAvatarClass(item.category)}`}
          onClick={onSelect}
          type="button"
        >
          {categoryInitial(item.category)}
        </button>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <button className="truncate text-left text-base font-semibold text-civic-heading hover:text-civic-primary" onClick={onSelect} type="button">
              {formatCategory(item.category)}
            </button>
            <span className="text-civic-muted">·</span>
            <span className="font-mono text-xs font-semibold text-civic-muted">{item.trackingCode}</span>
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-2 text-xs font-semibold text-civic-muted">
            <span className="inline-flex items-center gap-1">
              <MapPin className="h-3.5 w-3.5 text-civic-primary" aria-hidden="true" />
              {item.areaLabel}
            </span>
            <span>·</span>
            <span>{formatRelativeTime(item.createdAt)}</span>
          </div>
        </div>
        <button
          aria-label={`Open ${item.trackingCode} details`}
          className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-civic-muted transition hover:bg-civic-soft hover:text-civic-primary"
          onClick={onSelect}
          type="button"
        >
          <MoreHorizontal className="h-5 w-5" aria-hidden="true" />
        </button>
      </header>

      <div className="px-4 pb-4">
        <p className="break-words text-base leading-7 text-civic-heading">{item.description}</p>

        <div className="mt-3 flex flex-wrap gap-2">
          <StatusBadge status={status} />
          <SeverityBadge severity={severity} />
          <span className="rounded-md bg-civic-soft px-2 py-1 text-xs font-semibold text-civic-primary">Next: {nextStep}</span>
          <span className="rounded-md border border-civic-border bg-civic-raised px-2 py-1 text-xs font-semibold text-civic-muted">
            {item.agencyCode ?? "Routing pending"}
          </span>
          {item.isDuplicate ? <span className="rounded-md bg-status-review px-2 py-1 text-xs font-semibold text-status-review-text">Duplicate signal</span> : null}
        </div>

        <div className="mt-4 overflow-hidden rounded-lg border border-civic-border bg-civic-raised">
          {imageUrl ? (
            <FeedImagePreview imageUrl={imageUrl} item={item} mediaCount={mediaCount} onImageError={() => setImageFailed(true)} />
          ) : (
            <FeedVisualFallback item={item} mediaCount={mediaCount} />
          )}
          <div className="grid gap-3 border-t border-civic-border bg-civic-surface p-3 sm:grid-cols-3">
            <PostInfo icon={<Navigation className="h-4 w-4" aria-hidden="true" />} label="Approx. area" value={item.areaLabel} />
            <PostInfo icon={<Megaphone className="h-4 w-4" aria-hidden="true" />} label="Agency" value={item.agencyCode ?? "Pending"} />
            <PostInfo icon={<Eye className="h-4 w-4" aria-hidden="true" />} label="Visibility" value={item.hasReview ? "Reviewed" : "Public"} />
          </div>
        </div>

        {item.latestMediaSummary ? (
          <div className="mt-3 rounded-md bg-civic-soft p-3 text-sm leading-6 text-civic-muted">
            {item.latestMediaSummary}
          </div>
        ) : null}

        <div className="mt-3 flex items-center justify-between border-y border-civic-border py-2 text-xs font-semibold text-civic-muted">
          <span className="inline-flex items-center gap-2">
            <ThumbsUp className={`h-4 w-4 ${helpful ? "text-civic-primary" : ""}`} aria-hidden="true" />
            {helpful ? "Marked helpful" : "Community visibility"}
          </span>
          <span>{mediaCount} media · {item.hasReview ? "reviewed" : "awaiting review"}</span>
        </div>

        <footer className="grid grid-cols-2 gap-2 pt-3 sm:grid-cols-4">
          <PostAction active={helpful} icon={<Heart className="h-4 w-4" aria-hidden="true" />} label="Helpful" onClick={() => setHelpful((current) => !current)} />
          <Link
            className="inline-flex h-10 items-center justify-center gap-2 rounded-md text-sm font-semibold text-civic-muted transition hover:bg-civic-soft hover:text-civic-primary"
            href={`/public/incidents/${encodeURIComponent(item.trackingCode)}#comments`}
          >
            <MessageCircle className="h-4 w-4" aria-hidden="true" />
            Comment
          </Link>
          <PostAction icon={copied ? <Copy className="h-4 w-4" aria-hidden="true" /> : <Share2 className="h-4 w-4" aria-hidden="true" />} label={copied ? "Copied" : "Share"} onClick={() => void copyReportLink()} />
          <Link
            className="inline-flex h-10 items-center justify-center gap-2 rounded-md text-sm font-semibold text-civic-muted transition hover:bg-civic-soft hover:text-civic-primary"
            href={`/public/status?code=${encodeURIComponent(item.trackingCode)}`}
          >
            <Send className="h-4 w-4" aria-hidden="true" />
            Track
          </Link>
        </footer>
        <Link
          className="mt-2 inline-flex h-10 w-full items-center justify-center gap-2 rounded-md border border-civic-border px-3 text-sm font-semibold text-civic-primary transition hover:bg-civic-soft"
          href={`/public/incidents/${encodeURIComponent(item.trackingCode)}`}
        >
          <Bell className="h-4 w-4" aria-hidden="true" />
          Follow, comment, and view details
        </Link>
      </div>
    </article>
  );
}

function FeedImagePreview({
  imageUrl,
  item,
  mediaCount,
  onImageError,
}: {
  imageUrl: string;
  item: PublicIncidentFeedItemDto;
  mediaCount: number;
  onImageError: () => void;
}) {
  return (
    <div className="relative bg-black">
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img
        alt={`${formatCategory(item.category)} report evidence`}
        className="max-h-[560px] min-h-80 w-full bg-black object-contain sm:object-cover"
        loading="lazy"
        onError={onImageError}
        src={imageUrl}
      />
      <div className="pointer-events-none absolute inset-x-0 top-0 flex items-start justify-between gap-3 bg-gradient-to-b from-black/55 to-transparent p-3">
        <span className="inline-flex items-center gap-2 rounded-full bg-white/95 px-3 py-1.5 text-xs font-semibold text-civic-heading shadow-sm">
          <Camera className="h-4 w-4 text-civic-primary" aria-hidden="true" />
          Photo evidence
        </span>
        <span className="inline-flex items-center gap-2 rounded-full bg-civic-primary px-3 py-1.5 text-xs font-semibold text-white shadow-sm">
          <ImageIcon className="h-4 w-4" aria-hidden="true" />
          {mediaCount || 1} photo{(mediaCount || 1) === 1 ? "" : "s"}
        </span>
      </div>
      <div className="pointer-events-none absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/70 to-transparent p-4">
        <div className="max-w-xl rounded-lg bg-civic-surface/95 p-3 shadow-sm backdrop-blur">
          <div className="flex flex-wrap items-center gap-2 text-xs font-semibold text-civic-muted">
            <span className="inline-flex items-center gap-1 text-civic-primary">
              <MapPin className="h-3.5 w-3.5" aria-hidden="true" />
              {item.areaLabel}
            </span>
            <span>{formatRelativeTime(item.createdAt)}</span>
          </div>
          <p className="mt-1 line-clamp-2 text-sm font-semibold text-civic-heading">{item.description}</p>
        </div>
      </div>
    </div>
  );
}

function FeedVisualFallback({
  item,
  mediaCount,
}: {
  item: PublicIncidentFeedItemDto;
  mediaCount: number;
}) {
  const severity = normalizeSeverity(item.severity);
  const status = normalizeStatus(item.status);

  return (
    <div className="relative overflow-hidden bg-[#f3f8f6] p-4">
      <div className="absolute inset-0 [background-image:linear-gradient(rgba(42,129,110,0.10)_1px,transparent_1px),linear-gradient(90deg,rgba(42,129,110,0.10)_1px,transparent_1px)] [background-size:42px_42px]" />
      <div className="absolute left-[-12%] top-[42%] h-8 w-[130%] rotate-[-8deg] border-y border-civic-primary/20 bg-white/70" />
      <div className="relative grid min-h-72 gap-3 sm:grid-cols-[1.2fr_0.8fr]">
        <div className="flex flex-col justify-between rounded-lg border border-civic-border bg-civic-surface/95 p-4 shadow-sm">
          <div>
            <span className={`inline-flex h-12 w-12 items-center justify-center rounded-full text-base font-semibold text-white ${categoryAvatarClass(item.category)}`}>
              {categoryInitial(item.category)}
            </span>
            <p className="mt-4 text-xs font-semibold uppercase text-civic-primary">Community report</p>
            <h3 className="mt-2 text-2xl font-semibold text-civic-heading">{formatCategory(item.category)}</h3>
            <p className="mt-3 line-clamp-3 text-sm leading-6 text-civic-muted">{item.description}</p>
          </div>
          <div className="mt-4 flex flex-wrap gap-2">
            <StatusBadge status={status} />
            <SeverityBadge severity={severity} />
            <span className="rounded-md bg-civic-soft px-2 py-1 text-xs font-semibold text-civic-primary">
              {mediaCount ? `${mediaCount} media attached` : "No photo yet"}
            </span>
          </div>
        </div>
        <div className="relative overflow-hidden rounded-lg border border-civic-border bg-[#e8f2ef] shadow-sm">
          <div className="absolute inset-0 [background-image:linear-gradient(rgba(15,23,42,0.08)_1px,transparent_1px),linear-gradient(90deg,rgba(15,23,42,0.08)_1px,transparent_1px)] [background-size:32px_32px]" />
          <div className="absolute left-[-20%] top-[52%] h-6 w-[145%] rotate-[-12deg] bg-white/75" />
          <div className="absolute left-[58%] top-[-12%] h-[130%] w-5 rotate-[5deg] bg-white/80" />
          <div className="absolute left-1/2 top-1/2 flex h-16 w-16 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full border-4 border-white bg-civic-primary text-white shadow-lg">
            <MapPin className="h-7 w-7" aria-hidden="true" />
          </div>
          <div className="absolute bottom-3 left-3 right-3 rounded-lg bg-civic-surface/95 p-3 text-xs font-semibold text-civic-muted shadow-sm">
            <p className="truncate text-civic-heading">{item.areaLabel}</p>
            <p className="mt-1 font-mono">{item.trackingCode}</p>
          </div>
        </div>
      </div>
    </div>
  );
}

function CommunityMapPreview({
  items,
  onSelect,
  selectedTrackingCode,
}: {
  items: PublicIncidentFeedItemDto[];
  onSelect: (trackingCode: string) => void;
  selectedTrackingCode: string | null;
}) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<MapLibreMap | null>(null);
  const [mapReady, setMapReady] = useState(false);
  const [mapError, setMapError] = useState<string | null>(null);
  const visibleItems = items
    .filter(hasFeedCoordinate)
    .slice(0, 36);
  const selectedItem = visibleItems.find((item) => item.trackingCode === selectedTrackingCode) ?? visibleItems[0] ?? null;
  const feedMapData = useMemo(() => buildFeedMapFeatures(visibleItems), [visibleItems]);

  useEffect(() => {
    let disposed = false;

    async function mountMap() {
      try {
        setMapError(null);

        if (disposed || !containerRef.current) {
          return;
        }

        const map = new MapLibre({
          attributionControl: { compact: true },
          center: [defaultFeedMapCenter.longitude, defaultFeedMapCenter.latitude],
          cooperativeGestures: true,
          container: containerRef.current,
          maxZoom: 18,
          minZoom: 3,
          style: buildPublicFeedMapStyle(emptyFeedMapFeatures()),
          zoom: 11,
        });

        mapRef.current = map;
        map.addControl(new NavigationControl({ showCompass: false }), "top-right");
        map.addControl(new ScaleControl({ unit: "metric" }), "bottom-left");

        const finishInitialization = () => {
          if (disposed) {
            return;
          }

          map.resize();
          setMapReady(true);
          setMapError(null);
        };

        const handleMarkerClick = (event: MapLayerMouseEvent) => {
          const trackingCode = event.features?.[0]?.properties?.trackingCode;
          if (typeof trackingCode === "string" && trackingCode.length > 0) {
            onSelect(trackingCode);
          }
        };

        map.on("load", finishInitialization);
        map.on("click", feedMapLayerIds.marker, handleMarkerClick);
        map.on("mouseenter", feedMapLayerIds.marker, () => {
          map.getCanvas().style.cursor = "pointer";
        });
        map.on("mouseleave", feedMapLayerIds.marker, () => {
          map.getCanvas().style.cursor = "";
        });
        map.on("error", (event) => {
          if (!disposed && !map.loaded()) {
            setMapError(event.error?.message ?? "The interactive feed map could not be initialized.");
          }
        });

        window.requestAnimationFrame(() => {
          map.resize();
          if (map.loaded() || map.isStyleLoaded()) {
            finishInitialization();
          }
        });
      } catch {
        if (!disposed) {
          setMapError("The interactive feed map could not be initialized.");
        }
      }
    }

    void mountMap();

    return () => {
      disposed = true;
      mapRef.current?.remove();
      mapRef.current = null;
    };
  }, [onSelect]);

  useEffect(() => {
    const map = mapRef.current;
    if (!mapReady || !map?.isStyleLoaded()) {
      return;
    }

    const source = map.getSource(feedMapSourceId);
    if (source && "setData" in source) {
      (source as GeoJSONSource).setData(feedMapData);
    }

    const selectedFilter: FilterSpecification = ["==", ["get", "trackingCode"], selectedTrackingCode ?? ""];
    map.setFilter(feedMapLayerIds.selectedHalo, selectedFilter);
    map.setFilter(feedMapLayerIds.selectedRing, selectedFilter);

    if (selectedItem) {
      map.easeTo({
        center: [selectedItem.approximateLongitude, selectedItem.approximateLatitude],
        duration: 420,
        essential: true,
        zoom: Math.max(map.getZoom(), visibleItems.length === 1 ? 14 : 12),
      });
    } else {
      fitFeedMapItems(map, visibleItems);
    }
  }, [feedMapData, mapReady, selectedItem, selectedTrackingCode, visibleItems]);

  function fitReports() {
    const map = mapRef.current;
    if (!map) {
      return;
    }

    fitFeedMapItems(map, visibleItems);
  }

  return (
    <Panel
      action={
        <span className="inline-flex items-center gap-2 rounded-md bg-civic-soft px-3 py-2 text-xs font-semibold text-civic-primary">
          <MapIcon className="h-4 w-4" aria-hidden="true" />
          {visibleItems.length} pins
        </span>
      }
      description="Approximate public locations; exact report positions are privacy-protected."
      title="Community Map"
    >
      <div className="civic-map relative h-[430px] overflow-hidden rounded-lg border border-civic-border bg-[#eef6f3] shadow-sm">
        <div className="absolute inset-0" ref={containerRef} />

        <div className="pointer-events-none absolute left-3 right-3 top-3 z-10 flex flex-wrap items-start justify-between gap-2">
          <div className="max-w-[78%] rounded-md border border-civic-border bg-civic-surface/95 p-3 shadow-sm backdrop-blur">
            <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
              <span className="grid h-8 w-8 place-items-center rounded-md bg-civic-soft text-civic-primary">
                <MapIcon className="h-4 w-4" aria-hidden="true" />
              </span>
              <span>{selectedItem ? formatCategory(selectedItem.category) : "Public incident map"}</span>
            </div>
            <p className="mt-2 truncate text-xs font-semibold text-civic-muted">
              {selectedItem ? selectedItem.areaLabel : `${visibleItems.length} approximate public locations`}
            </p>
          </div>

          <button
            className="pointer-events-auto inline-flex h-10 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface/95 px-3 text-sm font-semibold text-civic-primary shadow-sm backdrop-blur transition hover:bg-white disabled:cursor-not-allowed disabled:opacity-60"
            disabled={!visibleItems.length}
            onClick={fitReports}
            type="button"
          >
            <MapPin className="h-4 w-4" aria-hidden="true" />
            Fit
          </button>
        </div>

        {selectedItem ? (
          <div className="absolute bottom-3 left-3 right-3 z-10 rounded-md border border-civic-border bg-civic-surface/95 p-3 shadow-sm backdrop-blur">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <p className="truncate text-sm font-semibold text-civic-heading">{formatCategory(selectedItem.category)}</p>
                <p className="mt-1 truncate text-xs font-semibold text-civic-muted">
                  {selectedItem.trackingCode} · {selectedItem.areaLabel}
                </p>
              </div>
              <SeverityBadge severity={normalizeSeverity(selectedItem.severity)} />
            </div>
          </div>
        ) : null}

        {!visibleItems.length ? (
          <div className="absolute inset-0 z-20 grid place-items-center bg-civic-surface/85 p-6 text-center text-sm font-semibold text-civic-muted backdrop-blur">
            No report locations match this view yet.
          </div>
        ) : null}

        {!mapReady && !mapError ? (
          <div className="absolute inset-0 z-20 grid place-items-center bg-civic-surface/80 p-6 text-center text-sm font-semibold text-civic-muted backdrop-blur">
            Loading interactive feed map...
          </div>
        ) : null}

        {mapError ? (
          <div className="absolute inset-0 z-20 grid place-items-center bg-civic-surface/90 p-6 text-center">
            <div className="max-w-sm rounded-md border border-status-critical bg-status-critical/10 p-4 text-sm font-semibold text-status-critical-text">
              {mapError}
            </div>
          </div>
        ) : null}
      </div>
    </Panel>
  );
}

function buildPublicFeedMapStyle(
  feedMapData: GeoJSON.FeatureCollection<GeoJSON.Point, FeedMapFeatureProperties>,
): StyleSpecification {
  return {
    version: 8,
    sources: {
      osm: {
        attribution: "© OpenStreetMap contributors",
        tileSize: 256,
        tiles: ["https://tile.openstreetmap.org/{z}/{x}/{y}.png"],
        type: "raster",
      },
      [feedMapSourceId]: {
        data: feedMapData,
        type: "geojson",
      },
    },
    layers: [
      {
        id: "public-feed-map-background",
        type: "background",
        paint: {
          "background-color": "#eef6f3",
        },
      },
      {
        id: "public-feed-map-osm",
        source: "osm",
        type: "raster",
        paint: {
          "raster-contrast": -0.03,
          "raster-opacity": 0.96,
          "raster-saturation": -0.18,
        },
      },
      {
        id: feedMapLayerIds.heat,
        maxzoom: 15,
        source: feedMapSourceId,
        type: "heatmap",
        paint: {
          "heatmap-color": [
            "interpolate",
            ["linear"],
            ["heatmap-density"],
            0,
            "rgba(35,123,103,0)",
            0.25,
            "rgba(35,123,103,0.24)",
            0.6,
            "rgba(129,91,8,0.34)",
            1,
            "rgba(155,47,35,0.42)",
          ],
          "heatmap-intensity": ["interpolate", ["linear"], ["zoom"], 8, 0.7, 14, 1.5],
          "heatmap-radius": ["interpolate", ["linear"], ["zoom"], 8, 20, 14, 42],
          "heatmap-weight": [
            "match",
            ["get", "severity"],
            "Critical",
            1,
            "High",
            0.82,
            "Medium",
            0.55,
            0.32,
          ],
        },
      },
      {
        filter: ["==", ["get", "trackingCode"], ""],
        id: feedMapLayerIds.selectedHalo,
        source: feedMapSourceId,
        type: "circle",
        paint: {
          "circle-blur": 0.24,
          "circle-color": "#237b67",
          "circle-opacity": 0.24,
          "circle-radius": ["interpolate", ["linear"], ["zoom"], 9, 30, 15, 64],
        },
      },
      {
        id: feedMapLayerIds.marker,
        source: feedMapSourceId,
        type: "circle",
        paint: {
          "circle-color": [
            "match",
            ["get", "severity"],
            "Critical",
            "#9b2f23",
            "High",
            "#237b67",
            "Medium",
            "#815b08",
            "#234b9b",
          ],
          "circle-radius": ["interpolate", ["linear"], ["zoom"], 8, 5, 13, 8, 17, 12],
          "circle-stroke-color": "#ffffff",
          "circle-stroke-width": 3,
        },
      },
      {
        filter: ["==", ["get", "trackingCode"], ""],
        id: feedMapLayerIds.selectedRing,
        source: feedMapSourceId,
        type: "circle",
        paint: {
          "circle-color": "rgba(255,255,255,0)",
          "circle-radius": ["interpolate", ["linear"], ["zoom"], 8, 12, 13, 18, 17, 28],
          "circle-stroke-color": "#111815",
          "circle-stroke-opacity": 0.72,
          "circle-stroke-width": 3,
        },
      },
    ],
  };
}

function buildFeedMapFeatures(items: PublicIncidentFeedItemDto[]): GeoJSON.FeatureCollection<GeoJSON.Point, FeedMapFeatureProperties> {
  return {
    features: items.map((item) => ({
      geometry: {
        coordinates: [item.approximateLongitude, item.approximateLatitude],
        type: "Point",
      },
      properties: {
        areaLabel: item.areaLabel,
        category: item.category,
        description: item.description,
        severity: item.severity,
        status: item.status,
        trackingCode: item.trackingCode,
      },
      type: "Feature",
    })),
    type: "FeatureCollection",
  };
}

function emptyFeedMapFeatures(): GeoJSON.FeatureCollection<GeoJSON.Point, FeedMapFeatureProperties> {
  return {
    features: [],
    type: "FeatureCollection",
  };
}

function fitFeedMapItems(map: MapLibreMap, items: PublicIncidentFeedItemDto[]) {
  const validItems = items.filter(hasFeedCoordinate);

  if (validItems.length === 0) {
    map.easeTo({
      center: [defaultFeedMapCenter.longitude, defaultFeedMapCenter.latitude],
      duration: 420,
      essential: true,
      zoom: 11,
    });
    return;
  }

  if (validItems.length === 1) {
    const [item] = validItems;
    map.easeTo({
      center: [item.approximateLongitude, item.approximateLatitude],
      duration: 420,
      essential: true,
      zoom: 14,
    });
    return;
  }

  const longitudes = validItems.map((item) => item.approximateLongitude);
  const latitudes = validItems.map((item) => item.approximateLatitude);

  map.fitBounds(
    [
      [Math.min(...longitudes), Math.min(...latitudes)],
      [Math.max(...longitudes), Math.max(...latitudes)],
    ],
    {
      duration: 520,
      essential: true,
      maxZoom: 14.5,
      padding: 54,
    },
  );
}

function hasFeedCoordinate(item: PublicIncidentFeedItemDto) {
  return Number.isFinite(item.approximateLatitude)
    && Number.isFinite(item.approximateLongitude)
    && Math.abs(item.approximateLatitude) <= 90
    && Math.abs(item.approximateLongitude) <= 180;
}

function PostInfo({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  return (
    <div className="flex items-start gap-2">
      <span className="mt-0.5 text-civic-primary">{icon}</span>
      <span className="min-w-0">
        <span className="block text-xs font-semibold text-civic-muted">{label}</span>
        <span className="block truncate text-sm font-semibold text-civic-heading">{value}</span>
      </span>
    </div>
  );
}

function PostAction({
  active = false,
  icon,
  label,
  onClick,
}: {
  active?: boolean;
  icon: ReactNode;
  label: string;
  onClick: () => void;
}) {
  return (
    <button
      className={`inline-flex h-10 items-center justify-center gap-2 rounded-md text-sm font-semibold transition ${
        active ? "bg-civic-soft text-civic-primary" : "text-civic-muted hover:bg-civic-soft hover:text-civic-primary"
      }`}
      onClick={onClick}
      type="button"
    >
      {icon}
      {label}
    </button>
  );
}

function SelectedReport({ item }: { item: PublicIncidentFeedItemDto | null }) {
  if (!item) {
    return (
      <Panel title="Selected Report">
        <div className="rounded-md border border-civic-border bg-civic-raised p-4 text-sm text-civic-muted">
          Select a report from the feed.
        </div>
      </Panel>
    );
  }

  return (
    <Panel
      action={<StatusBadge status={normalizeStatus(item.status)} />}
      description={item.areaLabel}
      title="Selected Report"
    >
      <div className="space-y-4">
        <div>
          <p className="font-mono text-sm font-semibold text-civic-primary">{item.trackingCode}</p>
          <h2 className="mt-2 break-words text-2xl font-semibold text-civic-heading">{formatCategory(item.category)}</h2>
          <p className="mt-2 break-words text-sm leading-6 text-civic-muted">{item.description}</p>
        </div>

        <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-1">
          <PublicDetail label="Agency" value={item.agencyCode ?? "Routing pending"} />
          <PublicDetail label="Severity" value={formatCategory(item.severity)} />
          <PublicDetail label="Approx. location" value={`${item.approximateLatitude.toFixed(3)}, ${item.approximateLongitude.toFixed(3)}`} />
          <PublicDetail label="Reported" value={formatDateTime(item.createdAt)} />
        </dl>

        <div className="rounded-md border border-civic-border bg-civic-raised p-4">
          <div className="text-sm font-semibold text-civic-heading">Public Workflow</div>
          <div className="mt-3 grid gap-2">
            {buildPublicWorkflow(item).map((step) => (
              <div className="flex items-start gap-3 rounded-md bg-civic-surface px-3 py-2" key={step.label}>
                <CheckCircle2
                  className={`mt-0.5 h-4 w-4 shrink-0 ${step.complete ? "text-civic-primary" : "text-civic-muted"}`}
                  aria-hidden="true"
                />
                <div>
                  <div className="text-sm font-semibold text-civic-heading">{step.label}</div>
                  <div className="text-xs leading-5 text-civic-muted">{step.detail}</div>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="grid gap-2">
          <Link
            className="inline-flex h-11 items-center justify-center gap-2 rounded-md bg-civic-primary px-4 text-sm font-semibold text-white transition hover:bg-civic-primary-strong"
            href={`/public/incidents/${encodeURIComponent(item.trackingCode)}`}
          >
            Open Public Page
            <ArrowRight className="h-4 w-4" aria-hidden="true" />
          </Link>
          <Link
            className="inline-flex h-11 items-center justify-center gap-2 rounded-md border border-civic-border px-4 text-sm font-semibold text-civic-primary transition hover:bg-civic-soft"
            href={`/public/status?code=${encodeURIComponent(item.trackingCode)}`}
          >
            Track Report
          </Link>
          <Link
            className="inline-flex h-11 items-center justify-center gap-2 rounded-md border border-civic-border px-4 text-sm font-semibold text-civic-primary transition hover:bg-civic-soft"
            href="/public/report"
          >
            Add Another Report
          </Link>
        </div>
      </div>
    </Panel>
  );
}

function PublicDetail({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-civic-border bg-civic-raised p-3">
      <dt className="text-xs font-semibold uppercase tracking-[0.12em] text-civic-muted">{label}</dt>
      <dd className="mt-1 break-words text-sm font-semibold text-civic-heading">{value}</dd>
    </div>
  );
}

function PublicMetric({
  icon,
  label,
  tone = "default",
  value,
}: {
  icon: ReactNode;
  label: string;
  tone?: "default" | "alert" | "review" | "calm";
  value: string;
}) {
  const toneClass =
    tone === "alert"
      ? "bg-status-critical text-status-critical-text"
      : tone === "review"
        ? "bg-status-review text-status-review-text"
        : tone === "calm"
          ? "bg-status-approved text-status-approved-text"
          : "bg-civic-soft text-civic-primary";

  return (
    <div className="rounded-lg border border-civic-border bg-civic-surface p-5 shadow-sm transition duration-200 hover:-translate-y-0.5 hover:border-civic-border-strong hover:shadow-md">
      <div className="flex items-center justify-between gap-3">
        <span className="text-sm font-semibold text-civic-muted">{label}</span>
        <span className={`rounded-md p-2 ${toneClass}`}>{icon}</span>
      </div>
      <p className="mt-4 text-3xl font-semibold text-civic-heading">{value}</p>
    </div>
  );
}

function FeedSkeleton() {
  return (
    <div className="space-y-3">
      {[0, 1, 2].map((item) => (
        <div className="animate-pulse rounded-lg border border-civic-border bg-civic-surface p-4 shadow-sm" key={item}>
          <div className="flex flex-wrap gap-2">
            <div className="h-6 w-28 rounded-md bg-civic-border" />
            <div className="h-6 w-20 rounded-md bg-civic-border" />
            <div className="h-6 w-20 rounded-md bg-civic-border" />
          </div>
          <div className="mt-4 h-6 w-1/2 rounded-md bg-civic-border" />
          <div className="mt-3 h-4 w-full rounded-md bg-civic-border" />
          <div className="mt-2 h-4 w-2/3 rounded-md bg-civic-border" />
        </div>
      ))}
    </div>
  );
}

function EmptyFeed({ query }: { query: string }) {
  return (
    <div className="rounded-lg border border-dashed border-civic-border bg-civic-surface p-8 text-center">
      <Filter className="mx-auto h-8 w-8 text-civic-primary" aria-hidden="true" />
      <h2 className="mt-3 text-xl font-semibold text-civic-heading">No Reports Found</h2>
      <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-civic-muted">
        {query.trim() ? "Try a different search or status filter." : "There are no public reports in this status yet."}
      </p>
    </div>
  );
}

function countStatus(items: PublicIncidentFeedItemDto[], status: StatusFilter) {
  if (status === "All") {
    return items.length;
  }

  return items.filter((item) => normalizeStatus(item.status) === status).length;
}

function countFeedView(items: PublicIncidentFeedItemDto[], view: FeedView) {
  return items.filter((item) => matchesFeedView(item, view)).length;
}

function matchesFeedView(item: PublicIncidentFeedItemDto, view: FeedView) {
  if (view === "All") {
    return true;
  }

  if (view === "Photos") {
    return Boolean(item.latestImageUrl) || (item.mediaCount ?? 0) > 0;
  }

  if (view === "NeedsAction") {
    const status = normalizeStatus(item.status);
    return !item.hasReview && status !== "Approved" && status !== "Dispatched";
  }

  return item.isDuplicate;
}

function feedViewLabel(view: FeedView) {
  if (view === "Photos") {
    return "Photo reports";
  }

  if (view === "NeedsAction") {
    return "Needs action";
  }

  if (view === "Duplicates") {
    return "Duplicate signals";
  }

  return "All activity";
}

function feedViewIcon(view: FeedView) {
  if (view === "Photos") {
    return <FileImage className="h-4 w-4" aria-hidden="true" />;
  }

  if (view === "NeedsAction") {
    return <TrendingUp className="h-4 w-4" aria-hidden="true" />;
  }

  if (view === "Duplicates") {
    return <Route className="h-4 w-4" aria-hidden="true" />;
  }

  return <Layers className="h-4 w-4" aria-hidden="true" />;
}

function storyIcon(status: StatusFilter) {
  if (status === "All") {
    return <Layers className="h-4 w-4" aria-hidden="true" />;
  }

  if (status === "Submitted") {
    return <MessageSquareWarning className="h-4 w-4" aria-hidden="true" />;
  }

  if (status === "Triaged") {
    return <Sparkles className="h-4 w-4" aria-hidden="true" />;
  }

  if (status === "HumanReviewRequired") {
    return <ShieldCheck className="h-4 w-4" aria-hidden="true" />;
  }

  if (status === "Approved") {
    return <CheckCircle2 className="h-4 w-4" aria-hidden="true" />;
  }

  return <Navigation className="h-4 w-4" aria-hidden="true" />;
}

function buildSummary(items: PublicIncidentFeedItemDto[]) {
  return items.reduce(
    (summary, item) => {
      const status = normalizeStatus(item.status);
      const mediaCount = item.mediaCount ?? 0;

      return {
        duplicates: summary.duplicates + (item.isDuplicate ? 1 : 0),
        media: summary.media + mediaCount,
        needsAction: summary.needsAction + (!item.hasReview && status !== "Approved" && status !== "Dispatched" ? 1 : 0),
        open: summary.open + (status === "Dispatched" ? 0 : 1),
        photos: summary.photos + (item.latestImageUrl ? 1 : 0),
        reviewed: summary.reviewed + (item.hasReview ? 1 : 0),
      };
    },
    { duplicates: 0, media: 0, needsAction: 0, open: 0, photos: 0, reviewed: 0 },
  );
}

function buildCategoryCounts(items: PublicIncidentFeedItemDto[]) {
  const counts = new Map<string, number>();

  for (const item of items) {
    counts.set(item.category, (counts.get(item.category) ?? 0) + 1);
  }

  return [...counts.entries()].sort((left, right) => right[1] - left[1]).slice(0, 5);
}

function buildPublicWorkflow(item: PublicIncidentFeedItemDto) {
  const status = normalizeStatus(item.status);
  const triaged = status !== "Submitted";
  const approved = item.hasReview || status === "Approved" || status === "Dispatched";

  return [
    {
      complete: true,
      detail: `Received ${formatRelativeTime(item.createdAt)} with public tracking enabled.`,
      label: "Submitted",
    },
    {
      complete: triaged,
      detail: triaged ? `${formatCategory(item.category)} routed by AI-assisted triage.` : "AI triage is checking category, severity, and duplicates.",
      label: "AI triage",
    },
    {
      complete: approved,
      detail: approved ? "Staff review is recorded on the backend." : "A staff reviewer can confirm or correct this report.",
      label: "Review / approve",
    },
    {
      complete: status === "Dispatched",
      detail: status === "Dispatched" ? "The case has been dispatched for response." : "Approved reports move to agency assignment and dispatch.",
      label: "Agency response",
    },
  ];
}

function nextPublicStep(item: PublicIncidentFeedItemDto) {
  const status = normalizeStatus(item.status);

  if (status === "Submitted") {
    return "AI triage";
  }

  if (status === "Triaged" || status === "HumanReviewRequired") {
    return item.hasReview ? "Agency approval" : "Staff review";
  }

  if (status === "Approved") {
    return "Dispatch";
  }

  return "Field response";
}

function resolvePublicMediaUrl(value: string | null | undefined) {
  if (!value) {
    return null;
  }

  if (value.startsWith("/")) {
    return value;
  }

  try {
    const url = new URL(value);
    if (isLoopbackHost(url.hostname)) {
      return `${url.pathname}${url.search}`;
    }

    return value;
  } catch {
    return value;
  }
}

function isLoopbackHost(hostname: string) {
  return hostname === "localhost" || hostname === "127.0.0.1" || hostname === "::1";
}

function categoryAvatarClass(category: string) {
  const normalized = category.toLowerCase();

  if (normalized.includes("road") || normalized.includes("pothole")) {
    return "bg-civic-primary";
  }

  if (normalized.includes("flood") || normalized.includes("drain")) {
    return "bg-blue-700";
  }

  if (normalized.includes("noise")) {
    return "bg-status-review-text";
  }

  if (normalized.includes("sanitation") || normalized.includes("trash")) {
    return "bg-emerald-700";
  }

  if (normalized.includes("critical") || normalized.includes("hazard")) {
    return "bg-status-critical-text";
  }

  return "bg-slate-700";
}

function categoryInitial(category: string) {
  const words = formatCategory(category).split(" ").filter(Boolean);
  const first = words[0]?.[0] ?? "C";
  const second = words.length > 1 ? words[1]?.[0] : "";

  return `${first}${second}`.toUpperCase();
}

function normalizeStatus(status: string): IncidentStatus {
  return incidentStatuses.includes(status as IncidentStatus) ? (status as IncidentStatus) : "Submitted";
}

function normalizeSeverity(severity: string): Severity {
  return severities.includes(severity as Severity) ? (severity as Severity) : "Medium";
}

function formatCategory(value: string) {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function formatDateTime(value: string) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "Recently";
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(date);
}

function formatRelativeTime(value: string) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "Recently";
  }

  const diffSeconds = Math.round((date.getTime() - Date.now()) / 1000);
  const absoluteSeconds = Math.abs(diffSeconds);

  if (absoluteSeconds < 60) {
    return "Just now";
  }

  const formatter = new Intl.RelativeTimeFormat(undefined, { numeric: "auto" });
  if (absoluteSeconds < 3600) {
    return formatter.format(Math.round(diffSeconds / 60), "minute");
  }

  if (absoluteSeconds < 86_400) {
    return formatter.format(Math.round(diffSeconds / 3600), "hour");
  }

  return formatter.format(Math.round(diffSeconds / 86_400), "day");
}
