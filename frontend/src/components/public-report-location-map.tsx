"use client";

import type { GeoJSONSource, MapLibreMap, StyleSpecification } from "maplibre-gl";
import { LocateFixed, MapPin } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";

type PublicReportLocationMapProps = {
  latitude: number;
  longitude: number;
  subtitle?: string;
  title: string;
};

type ReportPointProperties = {
  subtitle: string;
  title: string;
};

const reportSourceId = "public-report-location";
const reportLayerIds = {
  halo: "public-report-location-halo",
  marker: "public-report-location-marker",
  ring: "public-report-location-ring",
} as const;

export function PublicReportLocationMap({ latitude, longitude, subtitle = "Report location", title }: PublicReportLocationMapProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<MapLibreMap | null>(null);
  const [mapReady, setMapReady] = useState(false);
  const [mapError, setMapError] = useState<string | null>(null);
  const hasCoordinates = isValidCoordinate(latitude, longitude);
  const reportPoint = useMemo(() => buildReportPoint(longitude, latitude, title, subtitle), [latitude, longitude, subtitle, title]);
  const coordinateLabel = hasCoordinates ? `${latitude.toFixed(5)}, ${longitude.toFixed(5)}` : "Location unavailable";

  useEffect(() => {
    let disposed = false;

    async function mountMap() {
      if (!hasCoordinates) {
        setMapError("This report does not have valid coordinates yet.");
        return;
      }

      try {
        setMapError(null);
        const maplibre = await import("maplibre-gl");

        if (disposed || !containerRef.current) {
          return;
        }

        const map = new maplibre.Map({
          attributionControl: { compact: true },
          bearing: -8,
          center: [longitude, latitude],
          cooperativeGestures: true,
          container: containerRef.current,
          maxZoom: 18,
          minZoom: 10,
          pitch: 38,
          style: buildPublicLocationMapStyle(reportPoint),
          zoom: 15,
        });

        mapRef.current = map;
        map.addControl(new maplibre.NavigationControl({ showCompass: false }), "bottom-right");
        map.addControl(new maplibre.ScaleControl({ unit: "metric" }), "bottom-left");

        let initialized = false;
        const finishInitialization = () => {
          if (disposed || initialized) {
            return;
          }

          initialized = true;
          map.resize();
          setMapReady(true);
          setMapError(null);
        };

        map.on("load", finishInitialization);
        map.on("idle", finishInitialization);
        map.on("styledata", () => {
          if (map.isStyleLoaded()) {
            finishInitialization();
          }
        });
        map.on("error", (event) => {
          if (!disposed && !initialized) {
            setMapError(event.error?.message ?? "The interactive location map could not be initialized.");
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
          setMapError("The interactive location map could not be initialized.");
        }
      }
    }

    void mountMap();

    return () => {
      disposed = true;
      mapRef.current?.remove();
      mapRef.current = null;
    };
  }, [hasCoordinates, latitude, longitude, reportPoint]);

  useEffect(() => {
    const map = mapRef.current;

    if (!mapReady || !map?.isStyleLoaded() || !hasCoordinates) {
      return;
    }

    const source = map.getSource(reportSourceId);

    if (source && "setData" in source) {
      (source as GeoJSONSource).setData(reportPoint);
    }

    map.easeTo({
      center: [longitude, latitude],
      duration: 420,
      essential: true,
      zoom: Math.max(map.getZoom(), 14.5),
    });
  }, [hasCoordinates, latitude, longitude, mapReady, reportPoint]);

  function fitToReport() {
    if (!mapRef.current || !hasCoordinates) {
      return;
    }

    mapRef.current.easeTo({
      bearing: -8,
      center: [longitude, latitude],
      duration: 420,
      essential: true,
      pitch: 38,
      zoom: 15,
    });
  }

  return (
    <div className="civic-map relative min-h-[300px] overflow-hidden rounded-md border border-civic-border bg-[#edf3f1]">
      <div className="absolute inset-0" ref={containerRef} />

      <div className="pointer-events-none absolute left-3 right-3 top-3 z-10 flex items-start justify-between gap-2">
        <div className="max-w-[78%] rounded-md border border-civic-border bg-civic-surface/94 p-3 shadow-sm backdrop-blur">
          <div className="flex items-center gap-2 text-sm font-semibold text-civic-heading">
            <span className="grid h-8 w-8 shrink-0 place-items-center rounded-md bg-civic-soft text-civic-primary">
              <MapPin className="h-4 w-4" aria-hidden="true" />
            </span>
            <span className="min-w-0 truncate">{title}</span>
          </div>
          <p className="mt-2 truncate text-xs font-semibold text-civic-muted">{coordinateLabel}</p>
          <p className="mt-1 truncate text-xs text-civic-muted">{subtitle}</p>
        </div>

        <button
          className="pointer-events-auto inline-flex h-9 shrink-0 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface/94 px-3 text-sm font-semibold text-civic-primary shadow-sm backdrop-blur transition hover:bg-white"
          onClick={fitToReport}
          type="button"
        >
          <LocateFixed className="h-4 w-4" aria-hidden="true" />
          Fit
        </button>
      </div>

      {!mapReady && !mapError ? (
        <div className="absolute inset-0 z-20 grid place-items-center bg-civic-surface/80 p-6 text-center text-sm font-semibold text-civic-muted backdrop-blur">
          Loading live report map...
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
  );
}

function buildPublicLocationMapStyle(
  reportPoint: GeoJSON.FeatureCollection<GeoJSON.Point, ReportPointProperties>,
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
      [reportSourceId]: {
        data: reportPoint,
        type: "geojson",
      },
    },
    layers: [
      {
        id: "public-report-background",
        type: "background",
        paint: {
          "background-color": "#edf3f1",
        },
      },
      {
        id: "public-report-osm",
        source: "osm",
        type: "raster",
        paint: {
          "raster-contrast": -0.02,
          "raster-opacity": 0.96,
          "raster-saturation": -0.16,
        },
      },
      {
        id: reportLayerIds.halo,
        source: reportSourceId,
        type: "circle",
        paint: {
          "circle-blur": 0.2,
          "circle-color": "#237b67",
          "circle-opacity": 0.18,
          "circle-radius": ["interpolate", ["linear"], ["zoom"], 11, 28, 16, 54],
        },
      },
      {
        id: reportLayerIds.ring,
        source: reportSourceId,
        type: "circle",
        paint: {
          "circle-color": "#ffffff",
          "circle-opacity": 0.72,
          "circle-radius": ["interpolate", ["linear"], ["zoom"], 11, 15, 16, 24],
          "circle-stroke-color": "#111815",
          "circle-stroke-opacity": 0.5,
          "circle-stroke-width": 3,
        },
      },
      {
        id: reportLayerIds.marker,
        source: reportSourceId,
        type: "circle",
        paint: {
          "circle-color": "#237b67",
          "circle-radius": ["interpolate", ["linear"], ["zoom"], 11, 7, 16, 12],
          "circle-stroke-color": "#ffffff",
          "circle-stroke-width": 3,
        },
      },
    ],
  };
}

function buildReportPoint(
  longitude: number,
  latitude: number,
  title: string,
  subtitle: string,
): GeoJSON.FeatureCollection<GeoJSON.Point, ReportPointProperties> {
  return {
    features: [
      {
        geometry: {
          coordinates: [longitude, latitude],
          type: "Point",
        },
        properties: {
          subtitle,
          title,
        },
        type: "Feature",
      },
    ],
    type: "FeatureCollection",
  };
}

function isValidCoordinate(latitude: number, longitude: number) {
  return Number.isFinite(latitude) && Number.isFinite(longitude) && Math.abs(latitude) <= 90 && Math.abs(longitude) <= 180;
}
