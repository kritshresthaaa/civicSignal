import http from "k6/http";
import { check, sleep } from "k6";

const apiBaseUrl = __ENV.API_BASE_URL || "http://localhost:5020";
const email = __ENV.OPERATOR_EMAIL || "operator@civicsignal.local";
const password = __ENV.OPERATOR_PASSWORD || "Operator123456!";

export const options = {
  thresholds: {
    http_req_failed: ["rate<0.03"],
    http_req_duration: ["p(95)<1200"],
  },
  scenarios: {
    staff_dashboard_reads: {
      executor: "constant-vus",
      duration: __ENV.DURATION || "1m",
      vus: Number(__ENV.VUS || 3),
    },
  },
};

export function setup() {
  const login = http.post(
    `${apiBaseUrl}/api/auth/login`,
    JSON.stringify({ email, password }),
    {
      headers: {
        "Content-Type": "application/json",
        "X-Correlation-ID": "k6-staff-login",
      },
    },
  );

  check(login, {
    "operator login succeeds": (response) => response.status === 200,
    "access token returned": (response) => Boolean(response.json("accessToken")),
  });

  return {
    accessToken: login.json("accessToken"),
  };
}

export default function (data) {
  const headers = {
    Authorization: `Bearer ${data.accessToken}`,
    "X-Correlation-ID": `k6-staff-${__VU}-${__ITER}`,
  };

  const requests = [
    ["/api/system/capabilities", null],
    ["/api/system/integrations", null],
    ["/api/system/runtime-policy", null],
    ["/api/incidents?pageSize=25", headers],
    ["/api/historical-complaints/summary?pageSize=1", null],
    ["/api/data-import-jobs?source=NYC311&pageSize=10", headers],
    ["/api/forecasting/incidents?historyDays=30&horizonDays=7", null],
    ["/api/ai-evaluations/baselines", headers],
  ];

  for (const [path, requestHeaders] of requests) {
    const response = http.get(`${apiBaseUrl}${path}`, {
      headers: requestHeaders || {
        "X-Correlation-ID": `k6-public-read-${__VU}-${__ITER}`,
      },
    });

    check(response, {
      [`${path} succeeded`]: (result) => result.status >= 200 && result.status < 300,
    });
  }

  sleep(1);
}
