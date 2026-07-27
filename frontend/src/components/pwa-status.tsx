"use client";

import { useEffect, useRef, useState } from "react";
import { Download, RefreshCw, Smartphone, Wifi, WifiOff } from "lucide-react";
import { useOnlineStatus } from "@/lib/pwa";

type ServiceWorkerState = "checking" | "ready" | "unsupported" | "dev-disabled" | "error";

type BeforeInstallPromptEvent = Event & {
  prompt: () => Promise<void>;
  userChoice: Promise<{
    outcome: "accepted" | "dismissed";
    platform: string;
  }>;
};

export function PwaStatus() {
  const online = useOnlineStatus();
  const installPromptRef = useRef<BeforeInstallPromptEvent | null>(null);
  const [serviceWorkerState, setServiceWorkerState] = useState<ServiceWorkerState>("checking");
  const [installState, setInstallState] = useState<"idle" | "available" | "installed" | "dismissed">("idle");

  useEffect(() => {
    const handleBeforeInstallPrompt = (event: Event) => {
      event.preventDefault();
      installPromptRef.current = event as BeforeInstallPromptEvent;
      setInstallState("available");
    };
    const handleInstalled = () => {
      installPromptRef.current = null;
      setInstallState("installed");
    };

    window.addEventListener("beforeinstallprompt", handleBeforeInstallPrompt);
    window.addEventListener("appinstalled", handleInstalled);

    return () => {
      window.removeEventListener("beforeinstallprompt", handleBeforeInstallPrompt);
      window.removeEventListener("appinstalled", handleInstalled);
    };
  }, []);

  useEffect(() => {
    const register = async () => {
      if (!("serviceWorker" in navigator)) {
        setServiceWorkerState("unsupported");
        return;
      }

      if (process.env.NODE_ENV === "development") {
        await clearDevelopmentServiceWorkers();
        setServiceWorkerState("dev-disabled");
        return;
      }

      try {
        await navigator.serviceWorker.register("/sw.js", {
          scope: "/",
          updateViaCache: "none",
        });
        await navigator.serviceWorker.ready;
        setServiceWorkerState("ready");
      } catch {
        setServiceWorkerState("error");
      }
    };

    void register();
  }, []);

  async function installApp() {
    const prompt = installPromptRef.current;
    if (!prompt) {
      return;
    }

    await prompt.prompt();
    const choice = await prompt.userChoice;
    installPromptRef.current = null;
    setInstallState(choice.outcome === "accepted" ? "installed" : "dismissed");
  }

  return (
    <div className="border-b border-civic-border bg-civic-raised">
      <div className="mx-auto flex max-w-7xl flex-col gap-3 px-4 py-3 text-sm sm:flex-row sm:items-center sm:justify-between sm:px-6 lg:px-8">
        <div className="flex flex-wrap items-center gap-2">
          <span
            className={`inline-flex items-center gap-2 rounded-md px-2.5 py-1.5 font-semibold ${
              online ? "bg-status-approved text-status-approved-text" : "bg-status-critical text-status-critical-text"
            }`}
          >
            {online ? <Wifi className="h-4 w-4" aria-hidden="true" /> : <WifiOff className="h-4 w-4" aria-hidden="true" />}
            {online ? "Online" : "Offline draft mode"}
          </span>
          <span className="inline-flex items-center gap-2 rounded-md bg-civic-soft px-2.5 py-1.5 font-semibold text-civic-primary">
            <Smartphone className="h-4 w-4" aria-hidden="true" />
            {serviceWorkerLabel(serviceWorkerState)}
          </span>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {installState === "available" ? (
            <button
              className="inline-flex h-9 items-center justify-center gap-2 rounded-md bg-civic-primary px-3 text-sm font-semibold text-white hover:bg-civic-primary-strong"
              onClick={installApp}
              type="button"
            >
              <Download className="h-4 w-4" aria-hidden="true" />
              Install App
            </button>
          ) : null}
          <button
            className="inline-flex h-9 items-center justify-center gap-2 rounded-md border border-civic-border bg-civic-surface px-3 text-sm font-semibold text-civic-primary hover:bg-white"
            onClick={() => navigator.serviceWorker?.getRegistration().then((registration) => registration?.update())}
            type="button"
          >
            <RefreshCw className="h-4 w-4" aria-hidden="true" />
            Refresh Cache
          </button>
        </div>
      </div>
    </div>
  );
}

function serviceWorkerLabel(state: ServiceWorkerState) {
  if (state === "ready") {
    return "Offline shell ready";
  }

  if (state === "dev-disabled") {
    return "Dev cache disabled";
  }

  if (state === "unsupported") {
    return "Browser cache only";
  }

  if (state === "error") {
    return "Offline shell unavailable";
  }

  return "Preparing offline shell";
}

async function clearDevelopmentServiceWorkers() {
  const registrations = await navigator.serviceWorker.getRegistrations();
  await Promise.all(registrations.map((registration) => registration.unregister()));

  if ("caches" in window) {
    const keys = await caches.keys();
    await Promise.all(keys.filter((key) => key.startsWith("civic-signal-")).map((key) => caches.delete(key)));
  }
}
