/* eslint-disable @next/next/no-img-element */
"use client";

import { Flame, GitMerge, History, Layers, LocateFixed, MapPin, Minus, Plus, Radar } from "lucide-react";
import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import type { HistoricalComplaintDto } from "@/lib/civic-api";
import type { IncidentRow, Severity } from "@/lib/civic-types";
import type { RealtimeConnectionState } from "@/lib/civic-realtime";

type IncidentOperationsMapProps = {
  historicalComplaints?: HistoricalComplaintDto[];
  incidents: IncidentRow[];
  onSelectIncident: (incidentId: string) => void;
  realtimeState?: RealtimeConnectionState;
  selectedIncidentId: string;
};

type MapSize = {
  height: number;
  width: number;
};

type MapViewState = {
  center: [number, number];
  zoom: number;
};

type ScreenPoint = {
  x: number;
  y: number;
};

type ProjectedIncident = {
  incident: IncidentRow;
  screen: ScreenPoint;
};

type ProjectedHistoricalComplaint = {
  complaint: HistoricalComplaintDto;
  screen: ScreenPoint;
};

type IncidentCluster = {
  center: [number, number];
  duplicateCount: number;
  id: string;
  incidents: IncidentRow[];
  maxSlaRisk: number;
  screen: ScreenPoint;
  severity: Severity;
};

type MapTile = {
  key: string;
  left: number;
  src: string;
  top: number;
};

type DuplicateScreenLink = {
  candidateId: string;
  id: string;
  path: string;
  score: number;
};

type CoordinateBounds = {
  maxLatitude: number;
  maxLongitude: number;
  minLatitude: number;
  minLongitude: number;
};

type ZoneCell = {
  id: string;
  label: ScreenPoint;
  points: string;
  pressure: number;
};

type RoadLine = {
  id: string;
  importance: "major" | "minor";
  points: ScreenPoint[];
};

type DragState = {
  centerWorld: ScreenPoint;
  pointerId: number;
  start: ScreenPoint;
  zoom: number;
};

const defaultMapSize: MapSize = { height: 520, width: 720 };
const defaultView: MapViewState = { center: [-74.006, 40.7128], zoom: 13 };
const maxLatitude = 85.05112878;
const maxZoom = 18;
const minZoom = 10;
const tileSize = 256;

const severityRank: Record<Severity, number> = {
  Critical: 4,
  High: 3,
  Medium: 2,
  Low: 1,
};

export function IncidentOperationsMap({
  historicalComplaints = [],
  incidents,
  onSelectIncident,
  realtimeState = "idle",
  selectedIncidentId,
}: IncidentOperationsMapProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const dragRef = useRef<DragState | null>(null);
  const fittedKeyRef = useRef("");
  const [isDragging, setIsDragging] = useState(false);
  const [mapSize, setMapSize] = useState<MapSize>(defaultMapSize);
  const [showClusters, setShowClusters] = useState(true);
  const [showDuplicateLinks, setShowDuplicateLinks] = useState(true);
  const [showHeat, setShowHeat] = useState(true);
  const [showHistorical, setShowHistorical] = useState(true);
  const [showPoints, setShowPoints] = useState(true);
  const [view, setView] = useState<MapViewState>(defaultView);

  const validIncidents = useMemo(() => incidents.filter(hasValidCoordinates), [incidents]);
  const validHistoricalComplaints = useMemo(() => historicalComplaints.filter(hasValidHistoricalCoordinates), [historicalComplaints]);
  const selectedIncident = useMemo(
    () => incidents.find((incident) => incident.id === selectedIncidentId) ?? validIncidents[0] ?? incidents[0],
    [incidents, selectedIncidentId, validIncidents],
  );
  const bounds = useMemo(() => computeBounds(validIncidents), [validIncidents]);
  const incidentKey = useMemo(
    () =>
      validIncidents
        .map((incident) => `${incident.id}:${incident.coordinates.latitude.toFixed(5)}:${incident.coordinates.longitude.toFixed(5)}`)
        .join("|"),
    [validIncidents],
  );

  useEffect(() => {
    const element = containerRef.current;

    if (!element || typeof ResizeObserver === "undefined") {
      return;
    }

    const updateSize = () => {
      const rect = element.getBoundingClientRect();

      if (rect.width > 0 && rect.height > 0) {
        setMapSize({ height: rect.height, width: rect.width });
      }
    };

    const observer = new ResizeObserver(updateSize);
    observer.observe(element);
    updateSize();

    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const nextKey = `${incidentKey}:${Math.round(mapSize.width)}x${Math.round(mapSize.height)}`;

    if (fittedKeyRef.current === nextKey) {
      return;
    }

    fittedKeyRef.current = nextKey;
    setView(fitViewToIncidents(validIncidents, mapSize));
  }, [incidentKey, mapSize, validIncidents]);

  useEffect(() => {
    if (!selectedIncident || !hasValidCoordinates(selectedIncident)) {
      return;
    }

    const animationFrame = window.requestAnimationFrame(() => {
      setView((current) => {
        const screen = projectCoordinate(
          [selectedIncident.coordinates.longitude, selectedIncident.coordinates.latitude],
          current,
          mapSize,
        );

        if (isScreenPointVisible(screen, mapSize, 72)) {
          return current;
        }

        return {
          center: [selectedIncident.coordinates.longitude, selectedIncident.coordinates.latitude],
          zoom: Math.max(current.zoom, 14),
        };
      });
    });

    return () => window.cancelAnimationFrame(animationFrame);
  }, [mapSize, selectedIncident]);

  const tiles = useMemo(() => buildVisibleTiles(view, mapSize), [mapSize, view]);
  const projectedIncidents = useMemo(
    () =>
      validIncidents
        .map<ProjectedIncident>((incident) => ({
          incident,
          screen: projectCoordinate([incident.coordinates.longitude, incident.coordinates.latitude], view, mapSize),
        }))
        .filter((point) => isScreenPointVisible(point.screen, mapSize, 96)),
    [mapSize, validIncidents, view],
  );
  const projectedHistoricalComplaints = useMemo(
    () =>
      validHistoricalComplaints
        .map<ProjectedHistoricalComplaint>((complaint) => ({
          complaint,
          screen: projectCoordinate([complaint.longitude, complaint.latitude], view, mapSize),
        }))
        .filter((point) => isScreenPointVisible(point.screen, mapSize, 36)),
    [mapSize, validHistoricalComplaints, view],
  );
  const clusters = useMemo(
    () => (showClusters ? buildScreenClusters(projectedIncidents) : projectedIncidents.map(projectedIncidentToCluster)),
    [projectedIncidents, showClusters],
  );
  const duplicateLinks = useMemo(() => buildDuplicateScreenLinks(validIncidents, view, mapSize), [mapSize, validIncidents, view]);
  const roadLines = useMemo(() => buildRoadLines(bounds, view, mapSize), [bounds, mapSize, view]);
  const zoneCells = useMemo(() => buildZoneCells(validIncidents, bounds, view, mapSize), [bounds, mapSize, validIncidents, view]);

  const duplicateLinkCount = duplicateLinks.length;
  const historicalCount = validHistoricalComplaints.length;
  const hotspotCount = validIncidents.filter((incident) => incident.slaRisk >= 70 || incident.severity === "Critical").length;

  const handleFitMap = () => {
    setView(fitViewToIncidents(validIncidents, mapSize));
  };

  const handlePointerDown = (event: React.PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0 || isMapControlTarget(event.target)) {
      return;
    }

    const centerWorld = lonLatToWorld(view.center[0], view.center[1], view.zoom);
    dragRef.current = {
      centerWorld,
      pointerId: event.pointerId,
      start: { x: event.clientX, y: event.clientY },
      zoom: view.zoom,
    };
    event.currentTarget.setPointerCapture(event.pointerId);
    setIsDragging(true);
  };

  const handlePointerMove = (event: React.PointerEvent<HTMLDivElement>) => {
    const dragState = dragRef.current;

    if (!dragState || dragState.pointerId !== event.pointerId) {
      return;
    }

    const dx = event.clientX - dragState.start.x;
    const dy = event.clientY - dragState.start.y;
    const center = worldToLonLat(dragState.centerWorld.x - dx, dragState.centerWorld.y - dy, dragState.zoom);

    setView({ center, zoom: dragState.zoom });
  };

  const handlePointerEnd = (event: React.PointerEvent<HTMLDivElement>) => {
    const dragState = dragRef.current;

    if (dragState?.pointerId === event.pointerId) {
      dragRef.current = null;
      setIsDragging(false);
    }
  };

  const handleWheel = (event: React.WheelEvent<HTMLDivElement>) => {
    if (isMapControlTarget(event.target)) {
      return;
    }

    event.preventDefault();
    zoomAtScreenPoint(event.deltaY < 0 ? 1 : -1, { x: event.nativeEvent.offsetX, y: event.nativeEvent.offsetY });
  };

  const zoomAtScreenPoint = (delta: number, screenPoint?: ScreenPoint) => {
    setView((current) => {
      const nextZoom = clampZoom(current.zoom + delta);

      if (nextZoom === current.zoom) {
        return current;
      }

      const anchor = screenPoint ?? { x: mapSize.width / 2, y: mapSize.height / 2 };
      const centerWorld = lonLatToWorld(current.center[0], current.center[1], current.zoom);
      const anchoredWorld = {
        x: centerWorld.x + anchor.x - mapSize.width / 2,
        y: centerWorld.y + anchor.y - mapSize.height / 2,
      };
      const anchoredCoordinate = worldToLonLat(anchoredWorld.x, anchoredWorld.y, current.zoom);
      const nextAnchoredWorld = lonLatToWorld(anchoredCoordinate[0], anchoredCoordinate[1], nextZoom);
      const nextCenterWorld = {
        x: nextAnchoredWorld.x - anchor.x + mapSize.width / 2,
        y: nextAnchoredWorld.y - anchor.y + mapSize.height / 2,
      };

      return {
        center: worldToLonLat(nextCenterWorld.x, nextCenterWorld.y, nextZoom),
        zoom: nextZoom,
      };
    });
  };

  const handleClusterClick = (cluster: IncidentCluster) => {
    onSelectIncident(cluster.incidents[0].id);

    if (cluster.incidents.length > 1 && showClusters) {
      setView((current) => ({
        center: cluster.center,
        zoom: Math.min(maxZoom, current.zoom + 1),
      }));
    }
  };

  return (
    <div
      className={`civic-map relative h-[clamp(430px,52vw,650px)] min-h-[430px] overflow-hidden rounded-md border border-civic-border bg-[#eaf2ef] shadow-inner touch-none ${
        isDragging ? "cursor-grabbing" : "cursor-grab"
      }`}
      onPointerCancel={handlePointerEnd}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerEnd}
      onWheel={handleWheel}
      ref={containerRef}
    >
      <div className="absolute inset-0 overflow-hidden bg-[#eaf2ef]">
        <div
          aria-hidden="true"
          className="absolute inset-0 opacity-60"
          style={{
            backgroundImage:
              "linear-gradient(rgba(255,255,255,0.72) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.72) 1px, transparent 1px)",
            backgroundSize: "72px 72px",
          }}
        />
        {tiles.map((tile) => (
          <img
            alt=""
            aria-hidden="true"
            className="absolute h-64 w-64 select-none object-cover opacity-[0.94]"
            draggable={false}
            key={tile.key}
            referrerPolicy="no-referrer"
            src={tile.src}
            style={{ left: tile.left, top: tile.top }}
          />
        ))}
      </div>

      <svg aria-hidden="true" className="pointer-events-none absolute inset-0 z-[1] h-full w-full">
        <defs>
          <filter height="220%" id="incident-map-heat-blur" width="220%" x="-60%" y="-60%">
            <feGaussianBlur stdDeviation="18" />
          </filter>
        </defs>

        {zoneCells.map((zone) => (
          <g key={zone.id}>
            <polygon fill={zoneFillColor(zone.pressure)} opacity="0.2" points={zone.points} stroke="#bfd0c8" strokeDasharray="7 7" strokeWidth="1" />
            {zone.pressure > 0 ? (
              <text fill="#8da29a" fontSize="11" fontWeight="700" textAnchor="middle" x={zone.label.x} y={zone.label.y}>
                Z{zone.id}
              </text>
            ) : null}
          </g>
        ))}

        {roadLines.map((line) => (
          <polyline
            fill="none"
            key={line.id}
            points={line.points.map((point) => `${point.x},${point.y}`).join(" ")}
            stroke={line.importance === "major" ? "#96aaa2" : "#ffffff"}
            strokeLinecap="round"
            strokeWidth={line.importance === "major" ? 4 : 2}
            opacity={line.importance === "major" ? 0.52 : 0.7}
          />
        ))}

        {showHeat
          ? clusters.map((cluster) => (
              <circle
                cx={cluster.screen.x}
                cy={cluster.screen.y}
                fill={heatColor(cluster)}
                filter="url(#incident-map-heat-blur)"
                key={`heat-${cluster.id}`}
                opacity={Math.min(0.62, 0.18 + cluster.maxSlaRisk / 180)}
                r={Math.min(98, 38 + cluster.maxSlaRisk / 2 + cluster.incidents.length * 4)}
              />
            ))
          : null}

        {showHistorical
          ? projectedHistoricalComplaints.slice(0, 900).map(({ complaint, screen }) => (
              <circle
                cx={screen.x}
                cy={screen.y}
                fill={historicalComplaintColor(complaint.category)}
                key={complaint.id}
                opacity="0.42"
                r="3.2"
                stroke="#ffffff"
                strokeOpacity="0.8"
                strokeWidth="1"
              />
            ))
          : null}

        {showDuplicateLinks
          ? duplicateLinks.map((link) => (
              <path
                d={link.path}
                fill="none"
                key={link.id}
                opacity={Math.min(0.86, Math.max(0.28, link.score))}
                stroke="#8a650d"
                strokeDasharray="7 5"
                strokeLinecap="round"
                strokeWidth={Math.max(1.5, link.score * 4)}
              />
            ))
          : null}
      </svg>

      {showPoints
        ? clusters.map((cluster) => {
            const isSelected = cluster.incidents.some((incident) => incident.id === selectedIncidentId);
            const tone = incidentTone(cluster.incidents[0], cluster.maxSlaRisk, cluster.duplicateCount);

            return (
              <button
                aria-label={
                  cluster.incidents.length > 1
                    ? `${cluster.incidents.length} incident cluster near ${cluster.center[1].toFixed(5)}, ${cluster.center[0].toFixed(5)}`
                    : `Select ${cluster.incidents[0].title}`
                }
                className={`absolute z-[3] grid -translate-x-1/2 -translate-y-1/2 place-items-center rounded-full border-[3px] text-xs font-black text-white shadow-[0_10px_28px_rgba(17,24,21,0.22)] transition hover:scale-110 focus:outline-none focus:ring-4 focus:ring-civic-primary/25 ${
                  isSelected ? "h-11 w-11 ring-4 ring-civic-heading/20" : cluster.incidents.length > 1 ? "h-10 w-10" : "h-8 w-8"
                }`}
                data-map-control="true"
                key={`cluster-${cluster.id}`}
                onClick={() => handleClusterClick(cluster)}
                style={{
                  backgroundColor: tone.fill,
                  borderColor: isSelected ? "#111815" : "#ffffff",
                  left: cluster.screen.x,
                  top: cluster.screen.y,
                }}
                title={cluster.incidents.length > 1 ? `${cluster.incidents.length} reports in this area` : cluster.incidents[0].title}
                type="button"
              >
                {cluster.incidents.length > 1 ? cluster.incidents.length : <span className="h-2.5 w-2.5 rounded-full bg-white/95" />}
              </button>
            );
          })
        : null}

      <div className="pointer-events-none absolute left-3 right-3 top-3 z-10 flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
        <div
          className="pointer-events-auto flex max-w-full flex-wrap gap-1.5 rounded-md border border-civic-border bg-civic-surface/94 p-1.5 shadow-sm backdrop-blur"
          data-map-control="true"
        >
          <MapToggleButton active={showHeat} icon={<Flame className="h-4 w-4" />} label="Heat" onClick={() => setShowHeat((current) => !current)} />
          <MapToggleButton active={showClusters} icon={<Radar className="h-4 w-4" />} label="Clusters" onClick={() => setShowClusters((current) => !current)} />
          <MapToggleButton active={showPoints} icon={<MapPin className="h-4 w-4" />} label="Pins" onClick={() => setShowPoints((current) => !current)} />
          <MapToggleButton
            active={showHistorical}
            icon={<History className="h-4 w-4" />}
            label="311"
            onClick={() => setShowHistorical((current) => !current)}
          />
          <MapToggleButton
            active={showDuplicateLinks}
            icon={<GitMerge className="h-4 w-4" />}
            label="Links"
            onClick={() => setShowDuplicateLinks((current) => !current)}
          />
        </div>

        <div className="pointer-events-auto flex items-center gap-2 self-start" data-map-control="true">
          <div className="hidden overflow-hidden rounded-md border border-civic-border bg-civic-surface/94 shadow-sm backdrop-blur sm:flex">
            <MapIconButton label="Zoom out" onClick={() => zoomAtScreenPoint(-1)}>
              <Minus className="h-4 w-4" />
            </MapIconButton>
            <MapIconButton label="Zoom in" onClick={() => zoomAtScreenPoint(1)}>
              <Plus className="h-4 w-4" />
            </MapIconButton>
          </div>
          <RealtimeMapBadge state={realtimeState} />
          <button
            className="inline-flex h-9 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface/94 px-3 text-sm font-semibold text-civic-primary shadow-sm backdrop-blur transition hover:bg-white"
            onClick={handleFitMap}
            type="button"
          >
            <LocateFixed className="h-4 w-4" aria-hidden="true" />
            <span className="hidden sm:inline">Fit</span>
          </button>
        </div>
      </div>

      <div className="pointer-events-none absolute bottom-3 left-3 right-3 z-10 flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div
          className="hidden max-w-[292px] flex-wrap gap-1.5 rounded-md border border-civic-border bg-civic-surface/94 p-2 text-[11px] font-semibold text-civic-muted shadow-sm backdrop-blur sm:flex"
          data-map-control="true"
        >
          <MapLegend color="bg-status-approved-text" label="Low" />
          <MapLegend color="bg-civic-primary" label="High" />
          <MapLegend color="bg-status-review-text" label="Review" />
          <MapLegend color="bg-status-critical-text" label="Critical" />
          <span className="inline-flex items-center gap-1 rounded-md bg-civic-soft px-2 py-1 text-civic-primary">
            <Layers className="h-3.5 w-3.5" aria-hidden="true" />
            {hotspotCount} hot
          </span>
          <span className="inline-flex items-center gap-1 rounded-md bg-civic-soft px-2 py-1 text-civic-primary">
            <GitMerge className="h-3.5 w-3.5" aria-hidden="true" />
            {duplicateLinkCount} links
          </span>
          {historicalCount ? (
            <span className="inline-flex items-center gap-1 rounded-md bg-civic-soft px-2 py-1 text-civic-primary">
              <History className="h-3.5 w-3.5" aria-hidden="true" />
              {historicalCount} 311
            </span>
          ) : null}
        </div>

        {selectedIncident ? (
          <button
            className="pointer-events-auto hidden max-w-[300px] items-center gap-2 rounded-md border border-civic-border bg-civic-surface/94 px-3 py-2 text-left shadow-sm backdrop-blur transition hover:bg-white md:flex"
            data-map-control="true"
            onClick={() => onSelectIncident(selectedIncident.id)}
            type="button"
          >
            <span className="grid h-8 w-8 shrink-0 place-items-center rounded-md bg-civic-soft text-civic-primary">
              <MapPin className="h-4 w-4" aria-hidden="true" />
            </span>
            <span className="min-w-0 flex-1">
              <span className="block truncate text-sm font-semibold text-civic-heading">{selectedIncident.title}</span>
              <span className="block truncate text-xs font-semibold text-civic-muted">{selectedIncident.location}</span>
            </span>
            <span className="shrink-0 rounded-md bg-civic-soft px-2 py-1 text-xs font-semibold text-civic-primary">{selectedIncident.slaRisk}% SLA</span>
          </button>
        ) : null}
      </div>

      <div className="pointer-events-none absolute bottom-2 right-2 z-10 rounded bg-white/80 px-1.5 py-0.5 text-[10px] font-semibold text-civic-muted shadow-sm">
        © OpenStreetMap
      </div>

      {validIncidents.length === 0 ? (
        <div className="absolute inset-0 z-20 grid place-items-center bg-civic-surface/70 p-6 text-center text-sm font-semibold text-civic-muted backdrop-blur-sm">
          No incidents match the current filters.
        </div>
      ) : null}
    </div>
  );
}

function MapToggleButton({
  active,
  icon,
  label,
  onClick,
}: {
  active: boolean;
  icon: ReactNode;
  label: string;
  onClick: () => void;
}) {
  return (
    <button
      aria-pressed={active}
      className={`inline-flex h-8 items-center justify-center gap-1.5 rounded-md px-2.5 text-xs font-semibold transition ${
        active ? "bg-civic-primary text-white shadow-sm" : "bg-civic-raised text-civic-muted hover:bg-civic-soft hover:text-civic-primary"
      }`}
      onClick={onClick}
      type="button"
    >
      {icon}
      <span className="hidden sm:inline">{label}</span>
    </button>
  );
}

function MapIconButton({ children, label, onClick }: { children: ReactNode; label: string; onClick: () => void }) {
  return (
    <button
      aria-label={label}
      className="grid h-9 w-9 place-items-center border-r border-civic-border text-civic-primary transition last:border-r-0 hover:bg-civic-soft"
      onClick={onClick}
      type="button"
    >
      {children}
    </button>
  );
}

function RealtimeMapBadge({ state }: { state: RealtimeConnectionState }) {
  const labelByState: Record<RealtimeConnectionState, string> = {
    connected: "Live",
    connecting: "Connecting",
    idle: "API",
    offline: "Offline",
    reconnecting: "Syncing",
  };
  const statusClassName =
    state === "connected"
      ? "border-civic-primary/30 bg-civic-soft text-civic-primary"
      : state === "reconnecting" || state === "connecting"
        ? "border-status-review/70 bg-status-review/40 text-status-review-text"
        : "border-civic-border bg-civic-surface/94 text-civic-muted";
  const dotClassName =
    state === "connected"
      ? "status-dot bg-civic-primary"
      : state === "reconnecting" || state === "connecting"
        ? "bg-status-review-text"
        : "bg-civic-border-strong";

  return (
    <span className={`inline-flex h-9 items-center gap-2 rounded-md border px-3 text-xs font-semibold shadow-sm backdrop-blur ${statusClassName}`}>
      <span className={`h-2 w-2 rounded-full ${dotClassName}`} />
      <span className="hidden sm:inline">{labelByState[state]}</span>
    </span>
  );
}

function MapLegend({ color, label }: { color: string; label: string }) {
  return (
    <span className="inline-flex items-center gap-2 rounded-md bg-civic-raised px-2 py-1">
      <span className={`h-3 w-3 rounded-full ${color}`} />
      {label}
    </span>
  );
}

function buildVisibleTiles(view: MapViewState, size: MapSize): MapTile[] {
  const zoom = clampZoom(Math.round(view.zoom));
  const tileCount = 2 ** zoom;
  const centerWorld = lonLatToWorld(view.center[0], view.center[1], zoom);
  const startX = centerWorld.x - size.width / 2;
  const startY = centerWorld.y - size.height / 2;
  const minTileX = Math.floor(startX / tileSize) - 1;
  const maxTileX = Math.floor((startX + size.width) / tileSize) + 1;
  const minTileY = Math.max(0, Math.floor(startY / tileSize) - 1);
  const maxTileY = Math.min(tileCount - 1, Math.floor((startY + size.height) / tileSize) + 1);
  const tiles: MapTile[] = [];

  for (let x = minTileX; x <= maxTileX; x++) {
    for (let y = minTileY; y <= maxTileY; y++) {
      const wrappedX = wrapTileX(x, tileCount);

      tiles.push({
        key: `${zoom}-${x}-${y}`,
        left: x * tileSize - startX,
        src: `https://tile.openstreetmap.org/${zoom}/${wrappedX}/${y}.png`,
        top: y * tileSize - startY,
      });
    }
  }

  return tiles;
}

function buildScreenClusters(projectedIncidents: ProjectedIncident[]): IncidentCluster[] {
  const clusters: IncidentCluster[] = [];

  projectedIncidents.forEach((projectedIncident) => {
    const threshold = projectedIncident.incident.slaRisk >= 70 ? 52 : 44;
    const existingCluster = clusters.find((cluster) => distanceBetween(cluster.screen, projectedIncident.screen) <= threshold);

    if (!existingCluster) {
      clusters.push(projectedIncidentToCluster(projectedIncident));
      return;
    }

    existingCluster.incidents.push(projectedIncident.incident);
    const count = existingCluster.incidents.length;
    existingCluster.screen = {
      x: (existingCluster.screen.x * (count - 1) + projectedIncident.screen.x) / count,
      y: (existingCluster.screen.y * (count - 1) + projectedIncident.screen.y) / count,
    };
    existingCluster.center = averageIncidentCenter(existingCluster.incidents);
    existingCluster.duplicateCount += projectedIncident.incident.duplicates.length;
    existingCluster.maxSlaRisk = Math.max(existingCluster.maxSlaRisk, projectedIncident.incident.slaRisk);
    existingCluster.severity = highestSeverity(existingCluster.incidents);
    existingCluster.id = existingCluster.incidents.map((incident) => incident.id.slice(0, 8)).join("-");
  });

  return clusters.sort((left, right) => right.incidents.length - left.incidents.length || right.maxSlaRisk - left.maxSlaRisk);
}

function projectedIncidentToCluster(projectedIncident: ProjectedIncident): IncidentCluster {
  const { incident, screen } = projectedIncident;

  return {
    center: [incident.coordinates.longitude, incident.coordinates.latitude],
    duplicateCount: incident.duplicates.length,
    id: incident.id,
    incidents: [incident],
    maxSlaRisk: incident.slaRisk,
    screen,
    severity: incident.severity,
  };
}

function buildDuplicateScreenLinks(incidents: IncidentRow[], view: MapViewState, size: MapSize): DuplicateScreenLink[] {
  const byId = new Map(incidents.map((incident) => [incident.id, incident]));
  const links: DuplicateScreenLink[] = [];

  incidents.forEach((incident) => {
    if (!hasValidCoordinates(incident)) {
      return;
    }

    const startCoordinate: [number, number] = [incident.coordinates.longitude, incident.coordinates.latitude];
    const start = projectCoordinate(startCoordinate, view, size);

    incident.duplicates.forEach((duplicate, index) => {
      const candidate = byId.get(duplicate.caseId);
      const endCoordinate =
        candidate && hasValidCoordinates(candidate)
          ? ([candidate.coordinates.longitude, candidate.coordinates.latitude] as [number, number])
          : projectedCandidatePoint(startCoordinate, incident.id, duplicate.caseId, index);
      const end = projectCoordinate(endCoordinate, view, size);

      if (!isLineNearViewport(start, end, size)) {
        return;
      }

      links.push({
        candidateId: duplicate.caseId,
        id: `${incident.id}-${duplicate.caseId}-${index}`,
        path: curvedScreenPath(start, end, index),
        score: duplicate.score,
      });
    });
  });

  return links;
}

function buildRoadLines(bounds: CoordinateBounds, view: MapViewState, size: MapSize): RoadLine[] {
  const lines: RoadLine[] = [];

  for (let index = 1; index <= 8; index++) {
    const ratio = index / 9;
    const latitude = interpolate(bounds.minLatitude, bounds.maxLatitude, ratio);
    const longitude = interpolate(bounds.minLongitude, bounds.maxLongitude, ratio);
    const importance = index % 3 === 0 ? "major" : "minor";

    lines.push({
      id: `h-${index}`,
      importance,
      points: [
        projectCoordinate([bounds.minLongitude, latitude], view, size),
        projectCoordinate([bounds.maxLongitude, latitude + (index % 2 === 0 ? 0.0003 : -0.00025)], view, size),
      ],
    });
    lines.push({
      id: `v-${index}`,
      importance,
      points: [
        projectCoordinate([longitude, bounds.minLatitude], view, size),
        projectCoordinate([longitude + (index % 2 === 0 ? -0.00028 : 0.0003), bounds.maxLatitude], view, size),
      ],
    });
  }

  lines.push({
    id: "diagonal-main",
    importance: "major",
    points: [
      projectCoordinate([bounds.minLongitude, interpolate(bounds.minLatitude, bounds.maxLatitude, 0.28)], view, size),
      projectCoordinate([interpolate(bounds.minLongitude, bounds.maxLongitude, 0.54), interpolate(bounds.minLatitude, bounds.maxLatitude, 0.52)], view, size),
      projectCoordinate([bounds.maxLongitude, interpolate(bounds.minLatitude, bounds.maxLatitude, 0.67)], view, size),
    ],
  });

  return lines;
}

function buildZoneCells(incidents: IncidentRow[], bounds: CoordinateBounds, view: MapViewState, size: MapSize): ZoneCell[] {
  const cells: ZoneCell[] = [];

  for (let column = 0; column < 3; column++) {
    for (let row = 0; row < 2; row++) {
      const minLongitude = interpolate(bounds.minLongitude, bounds.maxLongitude, column / 3);
      const maxLongitude = interpolate(bounds.minLongitude, bounds.maxLongitude, (column + 1) / 3);
      const minLatitude = interpolate(bounds.minLatitude, bounds.maxLatitude, row / 2);
      const maxLatitude = interpolate(bounds.minLatitude, bounds.maxLatitude, (row + 1) / 2);
      const pressure = zonePressure(incidents, minLongitude, maxLongitude, minLatitude, maxLatitude);
      const points = [
        projectCoordinate([minLongitude, minLatitude], view, size),
        projectCoordinate([maxLongitude, minLatitude], view, size),
        projectCoordinate([maxLongitude, maxLatitude], view, size),
        projectCoordinate([minLongitude, maxLatitude], view, size),
      ];

      cells.push({
        id: `${row * 3 + column + 1}`,
        label: projectCoordinate([interpolate(minLongitude, maxLongitude, 0.5), interpolate(minLatitude, maxLatitude, 0.5)], view, size),
        points: points.map((point) => `${point.x},${point.y}`).join(" "),
        pressure,
      });
    }
  }

  return cells;
}

function fitViewToIncidents(incidents: IncidentRow[], size: MapSize): MapViewState {
  const validIncidents = incidents.filter(hasValidCoordinates);

  if (!validIncidents.length) {
    return defaultView;
  }

  if (validIncidents.length === 1) {
    const incident = validIncidents[0];

    return {
      center: [incident.coordinates.longitude, incident.coordinates.latitude],
      zoom: 15,
    };
  }

  const bounds = computeBounds(validIncidents);
  const center: [number, number] = [
    interpolate(bounds.minLongitude, bounds.maxLongitude, 0.5),
    interpolate(bounds.minLatitude, bounds.maxLatitude, 0.5),
  ];
  const usableWidth = Math.max(260, size.width - 170);
  const usableHeight = Math.max(260, size.height - 180);

  for (let zoom = maxZoom; zoom >= minZoom; zoom--) {
    const northWest = lonLatToWorld(bounds.minLongitude, bounds.maxLatitude, zoom);
    const southEast = lonLatToWorld(bounds.maxLongitude, bounds.minLatitude, zoom);

    if (Math.abs(southEast.x - northWest.x) <= usableWidth && Math.abs(southEast.y - northWest.y) <= usableHeight) {
      return { center, zoom };
    }
  }

  return { center, zoom: minZoom };
}

function projectCoordinate(coordinate: [number, number], view: MapViewState, size: MapSize): ScreenPoint {
  const zoom = clampZoom(Math.round(view.zoom));
  const centerWorld = lonLatToWorld(view.center[0], view.center[1], zoom);
  const pointWorld = lonLatToWorld(coordinate[0], coordinate[1], zoom);

  return {
    x: size.width / 2 + pointWorld.x - centerWorld.x,
    y: size.height / 2 + pointWorld.y - centerWorld.y,
  };
}

function lonLatToWorld(longitude: number, latitude: number, zoom: number): ScreenPoint {
  const clampedLatitude = Math.max(-maxLatitude, Math.min(maxLatitude, latitude));
  const scale = tileSize * 2 ** zoom;
  const sinLatitude = Math.sin((clampedLatitude * Math.PI) / 180);

  return {
    x: ((longitude + 180) / 360) * scale,
    y: (0.5 - Math.log((1 + sinLatitude) / (1 - sinLatitude)) / (4 * Math.PI)) * scale,
  };
}

function worldToLonLat(x: number, y: number, zoom: number): [number, number] {
  const scale = tileSize * 2 ** zoom;
  const longitude = (x / scale) * 360 - 180;
  const mercatorY = 0.5 - y / scale;
  const latitude = 90 - (360 * Math.atan(Math.exp(-mercatorY * 2 * Math.PI))) / Math.PI;

  return [wrapLongitude(longitude), Math.max(-maxLatitude, Math.min(maxLatitude, latitude))];
}

function computeBounds(incidents: IncidentRow[]): CoordinateBounds {
  const validIncidents = incidents.filter(hasValidCoordinates);

  if (!validIncidents.length) {
    return {
      maxLatitude: 40.719,
      maxLongitude: -73.998,
      minLatitude: 40.707,
      minLongitude: -74.014,
    };
  }

  const latitudes = validIncidents.map((incident) => incident.coordinates.latitude);
  const longitudes = validIncidents.map((incident) => incident.coordinates.longitude);
  const minLatitude = Math.min(...latitudes);
  const maxLatitudeValue = Math.max(...latitudes);
  const minLongitude = Math.min(...longitudes);
  const maxLongitudeValue = Math.max(...longitudes);
  const latitudePadding = Math.max(0.004, (maxLatitudeValue - minLatitude) * 0.36);
  const longitudePadding = Math.max(0.004, (maxLongitudeValue - minLongitude) * 0.36);

  return {
    maxLatitude: maxLatitudeValue + latitudePadding,
    maxLongitude: maxLongitudeValue + longitudePadding,
    minLatitude: minLatitude - latitudePadding,
    minLongitude: minLongitude - longitudePadding,
  };
}

function hasValidCoordinates(incident: IncidentRow) {
  const { latitude, longitude } = incident.coordinates;

  return Number.isFinite(latitude) && Number.isFinite(longitude) && Math.abs(latitude) <= 90 && Math.abs(longitude) <= 180;
}

function hasValidHistoricalCoordinates(complaint: HistoricalComplaintDto) {
  return (
    Number.isFinite(complaint.latitude) &&
    Number.isFinite(complaint.longitude) &&
    Math.abs(complaint.latitude) <= 90 &&
    Math.abs(complaint.longitude) <= 180
  );
}

function averageIncidentCenter(incidents: IncidentRow[]): [number, number] {
  const total = incidents.reduce(
    (sum, incident) => ({
      latitude: sum.latitude + incident.coordinates.latitude,
      longitude: sum.longitude + incident.coordinates.longitude,
    }),
    { latitude: 0, longitude: 0 },
  );

  return [total.longitude / incidents.length, total.latitude / incidents.length];
}

function highestSeverity(incidents: IncidentRow[]): Severity {
  return incidents.reduce<Severity>(
    (highest, incident) => (severityRank[incident.severity] > severityRank[highest] ? incident.severity : highest),
    "Low",
  );
}

function zonePressure(
  incidents: IncidentRow[],
  minLongitude: number,
  maxLongitudeValue: number,
  minLatitude: number,
  maxLatitudeValue: number,
) {
  const matchingIncidents = incidents.filter((incident) => {
    const { latitude, longitude } = incident.coordinates;

    return longitude >= minLongitude && longitude <= maxLongitudeValue && latitude >= minLatitude && latitude <= maxLatitudeValue;
  });

  if (!matchingIncidents.length) {
    return 0;
  }

  return Math.min(100, matchingIncidents.reduce((total, incident) => total + incident.slaRisk, 0) / matchingIncidents.length);
}

function projectedCandidatePoint(start: [number, number], incidentId: string, candidateId: string, index: number): [number, number] {
  const hash = Array.from(`${incidentId}:${candidateId}`).reduce((total, character) => total + character.charCodeAt(0), 0);
  const angle = ((hash % 360) * Math.PI) / 180;
  const distance = 0.0012 + index * 0.00035;

  return [start[0] + Math.cos(angle) * distance, start[1] + Math.sin(angle) * distance];
}

function curvedScreenPath(start: ScreenPoint, end: ScreenPoint, index: number) {
  const midX = interpolate(start.x, end.x, 0.5);
  const midY = interpolate(start.y, end.y, 0.5);
  const dx = end.x - start.x;
  const dy = end.y - start.y;
  const length = Math.max(1, Math.hypot(dx, dy));
  const curve = (index % 2 === 0 ? 1 : -1) * Math.min(50, Math.max(18, length * 0.16));
  const controlX = midX - (dy / length) * curve;
  const controlY = midY + (dx / length) * curve;

  return `M ${start.x} ${start.y} Q ${controlX} ${controlY} ${end.x} ${end.y}`;
}

function isLineNearViewport(start: ScreenPoint, end: ScreenPoint, size: MapSize) {
  return isScreenPointVisible(start, size, 160) || isScreenPointVisible(end, size, 160);
}

function isScreenPointVisible(point: ScreenPoint, size: MapSize, margin = 0) {
  return point.x >= -margin && point.x <= size.width + margin && point.y >= -margin && point.y <= size.height + margin;
}

function isMapControlTarget(target: EventTarget | null) {
  return target instanceof Element && Boolean(target.closest("[data-map-control='true']"));
}

function incidentTone(incident: IncidentRow, slaRisk: number, duplicateCount: number) {
  if (incident.severity === "Critical" || slaRisk >= 88) {
    return { fill: "#9b2f23" };
  }

  if (incident.status === "HumanReviewRequired" || duplicateCount > 0) {
    return { fill: "#8a650d" };
  }

  if (incident.severity === "High" || slaRisk >= 65) {
    return { fill: "#237b67" };
  }

  if (incident.status === "Approved") {
    return { fill: "#234b9b" };
  }

  return { fill: "#146b55" };
}

function heatColor(cluster: IncidentCluster) {
  if (cluster.severity === "Critical" || cluster.maxSlaRisk >= 88) {
    return "rgba(155,47,35,0.64)";
  }

  if (cluster.duplicateCount > 0 || cluster.maxSlaRisk >= 70) {
    return "rgba(138,101,13,0.56)";
  }

  return "rgba(35,123,103,0.5)";
}

function historicalComplaintColor(category: string) {
  switch (category) {
    case "RoadDamage":
      return "#344b9b";
    case "Flooding":
      return "#0e7490";
    case "Streetlight":
      return "#9a6b00";
    case "Sanitation":
      return "#63776f";
    default:
      return "#64748b";
  }
}

function zoneFillColor(pressure: number) {
  if (pressure >= 80) {
    return "#fff1cc";
  }

  if (pressure >= 50) {
    return "#e8f0ff";
  }

  if (pressure > 0) {
    return "#def3ec";
  }

  return "#f9fbfa";
}

function distanceBetween(left: ScreenPoint, right: ScreenPoint) {
  return Math.hypot(left.x - right.x, left.y - right.y);
}

function clampZoom(zoom: number) {
  return Math.max(minZoom, Math.min(maxZoom, zoom));
}

function wrapTileX(x: number, tileCount: number) {
  return ((x % tileCount) + tileCount) % tileCount;
}

function wrapLongitude(longitude: number) {
  return ((((longitude + 180) % 360) + 360) % 360) - 180;
}

function interpolate(start: number, end: number, ratio: number) {
  return start + (end - start) * ratio;
}
