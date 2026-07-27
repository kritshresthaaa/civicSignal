import type { NextConfig } from "next";

const backendOrigin = firstNonBlank(
  process.env.CIVIC_PROXY_API_BASE_URL,
  process.env.NEXT_PUBLIC_API_BASE_URL,
  "http://localhost:5020",
).replace(/\/$/, "");

const objectStorageOrigin = firstNonBlank(
  process.env.CIVIC_PROXY_OBJECT_STORAGE_BASE_URL,
  process.env.CIVIC_PROXY_MEDIA_BASE_URL,
  "http://localhost:9000",
).replace(/\/$/, "");

function firstNonBlank(...values: Array<string | undefined>) {
  return values.find((value) => value && value.trim().length > 0)?.trim() ?? "";
}

const nextConfig: NextConfig = {
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: `${backendOrigin}/api/:path*`,
      },
      {
        source: "/hubs/:path*",
        destination: `${backendOrigin}/hubs/:path*`,
      },
      {
        source: "/media/:path*",
        destination: `${backendOrigin}/media/:path*`,
      },
      {
        source: "/civic-signal/:path*",
        destination: `${objectStorageOrigin}/civic-signal/:path*`,
      },
      {
        source: "/health/:path*",
        destination: `${backendOrigin}/health/:path*`,
      },
    ];
  },
};

export default nextConfig;
