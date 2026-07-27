export type BrowserNotificationPermission = NotificationPermission | "unsupported";

export function getBrowserNotificationPermission(): BrowserNotificationPermission {
  if (typeof window === "undefined" || !("Notification" in window)) {
    return "unsupported";
  }

  return Notification.permission;
}

export async function requestBrowserNotificationPermission(): Promise<BrowserNotificationPermission> {
  if (typeof window === "undefined" || !("Notification" in window)) {
    return "unsupported";
  }

  if (Notification.permission !== "default") {
    return Notification.permission;
  }

  return Notification.requestPermission();
}

export async function showReportNotification({
  body,
  title,
  trackingCode,
}: {
  body: string;
  title: string;
  trackingCode?: string | null;
}) {
  if (getBrowserNotificationPermission() !== "granted") {
    return false;
  }

  const url = trackingCode ? `/public/status?code=${encodeURIComponent(trackingCode)}` : "/public/status";
  const options: NotificationOptions = {
    body,
    data: {
      url,
    },
    icon: "/icon.svg",
    tag: trackingCode ? `civic-signal-${trackingCode}` : "civic-signal-update",
  };

  const registration = await getReadyServiceWorkerRegistration();

  if (registration) {
    await registration.showNotification(title, options);
    return true;
  }

  new Notification(title, options);
  return true;
}

async function getReadyServiceWorkerRegistration() {
  if (typeof navigator === "undefined" || !("serviceWorker" in navigator)) {
    return null;
  }

  try {
    return await Promise.race<ServiceWorkerRegistration | null>([
      navigator.serviceWorker.ready,
      new Promise((resolve) => window.setTimeout(() => resolve(null), 800)),
    ]);
  } catch {
    return null;
  }
}
