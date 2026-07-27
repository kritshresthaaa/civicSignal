const CACHE_VERSION = "civic-signal-v3";
const APP_SHELL_CACHE = `${CACHE_VERSION}-app-shell`;
const RUNTIME_CACHE = `${CACHE_VERSION}-runtime`;
const STATIC_CACHE = `${CACHE_VERSION}-static`;
const LOCAL_DEVELOPMENT_HOSTS = new Set(["localhost", "127.0.0.1", "0.0.0.0"]);

const APP_SHELL_ROUTES = [
  "/",
  "/public",
  "/public/report",
  "/public/status",
  "/public/help",
  "/offline.html",
  "/manifest.webmanifest",
  "/icon.svg",
];

self.addEventListener("install", (event) => {
  if (LOCAL_DEVELOPMENT_HOSTS.has(self.location.hostname)) {
    event.waitUntil(self.skipWaiting());
    return;
  }

  event.waitUntil(
    caches
      .open(APP_SHELL_CACHE)
      .then((cache) => Promise.allSettled(APP_SHELL_ROUTES.map((route) => cache.add(route))))
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener("activate", (event) => {
  if (LOCAL_DEVELOPMENT_HOSTS.has(self.location.hostname)) {
    event.waitUntil(clearCivicSignalCaches().then(() => self.registration.unregister()));
    return;
  }

  event.waitUntil(
    clearOldCaches()
      .then(() => self.clients.claim()),
  );
});

self.addEventListener("fetch", (event) => {
  if (LOCAL_DEVELOPMENT_HOSTS.has(self.location.hostname)) {
    return;
  }

  const { request } = event;

  if (request.method !== "GET") {
    return;
  }

  const url = new URL(request.url);

  if (url.origin !== self.location.origin) {
    return;
  }

  if (url.pathname.startsWith("/api/")) {
    return;
  }

  if (url.pathname.startsWith("/_next/")) {
    return;
  }

  if (request.mode === "navigate") {
    event.respondWith(networkFirstNavigation(request));
    return;
  }

  if (url.pathname === "/icon.svg" || url.pathname === "/manifest.webmanifest") {
    event.respondWith(staleWhileRevalidate(request, STATIC_CACHE));
    return;
  }

  event.respondWith(cacheFirst(request, RUNTIME_CACHE));
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();

  const notificationUrl = event.notification.data?.url || "/public/status";
  const targetUrl = new URL(notificationUrl, self.location.origin).href;

  event.waitUntil(
    self.clients
      .matchAll({
        includeUncontrolled: true,
        type: "window",
      })
      .then((clientList) => {
        const matchingClient = clientList.find((client) => client.url === targetUrl);

        if (matchingClient) {
          return matchingClient.focus();
        }

        return self.clients.openWindow(targetUrl);
      }),
  );
});

async function networkFirstNavigation(request) {
  const cache = await caches.open(RUNTIME_CACHE);

  try {
    const response = await fetch(request);
    if (response.ok) {
      await cache.put(request, response.clone());
    }

    return response;
  } catch {
    const url = new URL(request.url);

    return (await cache.match(request)) || (await caches.match(url.pathname)) || (await caches.match("/offline.html"));
  }
}

async function staleWhileRevalidate(request, cacheName) {
  const cache = await caches.open(cacheName);
  const cached = await cache.match(request);
  const network = fetch(request)
    .then((response) => {
      if (response.ok) {
        cache.put(request, response.clone());
      }

      return response;
    })
    .catch(() => cached);

  return cached || network;
}

async function cacheFirst(request, cacheName) {
  const cache = await caches.open(cacheName);
  const cached = await cache.match(request);

  if (cached) {
    return cached;
  }

  const response = await fetch(request);
  if (response.ok) {
    await cache.put(request, response.clone());
  }

  return response;
}

async function clearOldCaches() {
  const keys = await caches.keys();

  await Promise.all(
    keys
      .filter((key) => ![APP_SHELL_CACHE, RUNTIME_CACHE, STATIC_CACHE].includes(key))
      .map((key) => caches.delete(key)),
  );
}

async function clearCivicSignalCaches() {
  const keys = await caches.keys();

  await Promise.all(keys.filter((key) => key.startsWith("civic-signal-")).map((key) => caches.delete(key)));
}
