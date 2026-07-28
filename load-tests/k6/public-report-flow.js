import http from "k6/http";
import { check, sleep } from "k6";

const apiBaseUrl = __ENV.API_BASE_URL || "http://localhost:5020";

export const options = {
  thresholds: {
    http_req_failed: ["rate<0.02"],
    http_req_duration: ["p(95)<900"],
  },
  scenarios: {
    public_report_flow: {
      executor: "constant-vus",
      duration: __ENV.DURATION || "1m",
      vus: Number(__ENV.VUS || 5),
    },
  },
};

export default function () {
  const suffix = `${__VU}-${__ITER}-${Date.now()}`;
  const latitude = 40.7128 + Math.random() / 1000;
  const longitude = -74.006 + Math.random() / 1000;

  const health = http.get(`${apiBaseUrl}/api/system/health`);
  check(health, {
    "system health reachable": (response) => response.status === 200,
    "correlation id returned": (response) => Boolean(response.headers["X-Correlation-ID"]),
  });

  const create = http.post(
    `${apiBaseUrl}/api/incidents`,
    JSON.stringify({
      description: `Load test pothole report ${suffix} near a crosswalk with visible road damage.`,
      latitude,
      longitude,
    }),
    {
      headers: {
        "Content-Type": "application/json",
        "X-Correlation-ID": `k6-public-${suffix}`,
      },
    },
  );

  check(create, {
    "incident created": (response) => response.status === 201 || response.status === 200,
    "tracking code returned": (response) => Boolean(response.json("trackingCode")),
  });

  const trackingCode = create.json("trackingCode");
  if (trackingCode) {
    const publicDetail = http.get(`${apiBaseUrl}/api/public/incidents/${trackingCode}`);
    check(publicDetail, {
      "public detail reachable": (response) => response.status === 200,
    });

    const status = http.get(`${apiBaseUrl}/api/public/incidents/${trackingCode}/status`);
    check(status, {
      "public status reachable": (response) => response.status === 200,
    });
  }

  const feed = http.get(`${apiBaseUrl}/api/public/incidents?pageSize=10`);
  check(feed, {
    "public feed reachable": (response) => response.status === 200,
  });

  sleep(1);
}
