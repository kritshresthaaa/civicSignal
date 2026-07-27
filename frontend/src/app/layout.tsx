import type { Metadata, Viewport } from "next";
import "./globals.css";

const developmentServiceWorkerCleanupScript = `
(() => {
  const localHosts = new Set(["localhost", "127.0.0.1", "0.0.0.0"]);

  if (!localHosts.has(window.location.hostname)) {
    return;
  }

  const reloadKey = "civic-signal-local-cache-cleared-v7";

  const clearServiceWorkerState = async () => {
    let cleared = false;

    if ("serviceWorker" in navigator) {
      const registrations = await navigator.serviceWorker.getRegistrations();
      if (registrations.length > 0) {
        cleared = true;
      }

      await Promise.all(registrations.map((registration) => registration.unregister()));
    }

    if ("caches" in window) {
      const keys = await caches.keys();
      const civicKeys = keys.filter((key) => key.startsWith("civic-signal-"));
      if (civicKeys.length > 0) {
        cleared = true;
      }

      await Promise.all(civicKeys.map((key) => caches.delete(key)));
    }

    return cleared;
  };

  const reloadOnce = () => {
    if (sessionStorage.getItem(reloadKey) === "true") {
      return;
    }

    sessionStorage.setItem(reloadKey, "true");
    window.location.reload();
  };

  window.addEventListener(
    "error",
    (event) => {
      const target = event.target;
      const assetUrl =
        target instanceof HTMLScriptElement || target instanceof HTMLLinkElement
          ? target.src || target.href
          : "";

      if (assetUrl.includes("/_next/")) {
        sessionStorage.removeItem(reloadKey);
        clearServiceWorkerState().finally(reloadOnce);
      }
    },
    true,
  );

  clearServiceWorkerState()
    .then((cleared) => {
      if (cleared) {
        reloadOnce();
      }
    })
    .catch(() => undefined);
})();
`;

export const metadata: Metadata = {
  applicationName: "CivicSignal AI",
  title: "CivicSignal AI",
  description: "Progressive web app for city incident reporting and AI-assisted operations.",
  manifest: "/manifest.webmanifest",
  appleWebApp: {
    capable: true,
    statusBarStyle: "default",
    title: "CivicSignal",
  },
  icons: {
    icon: "/icon.svg",
    shortcut: "/icon.svg",
  },
};

export const viewport: Viewport = {
  colorScheme: "light",
  themeColor: "#237b67",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="h-full antialiased">
      <head>
        <script
          dangerouslySetInnerHTML={{ __html: developmentServiceWorkerCleanupScript }}
          id="local-service-worker-cleanup"
        />
      </head>
      <body className="min-h-full">
        {children}
      </body>
    </html>
  );
}
