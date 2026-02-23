import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  scenarios: {
    load: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 20 },
        { duration: '60s', target: 50 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1200'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5033';

export function setup() {
  const res = http.post(`${BASE_URL}/api/v1/auth/token`, null);
  check(res, { 'token status 200': (r) => r.status === 200 });
  const body = res.json();
  return { token: body.access_token };
}

export default function (data) {
  const token = data.token;

  // Use a lot of distinct documentIds to exercise indexing and DB writes
  const docId = `DOC-LOAD-${__VU}-${__ITER % 1000}`;

  const payload = JSON.stringify({
    documentId: docId,
    amount: 10.0 + (__ITER % 100),
    currency: 'CLP',
  });

  // IMPORTANT: unique idempotency key per request, otherwise replays will skew the load test
  const idempotencyKey = `load-${__VU}-${__ITER}-${Date.now()}`;

  const res = http.post(`${BASE_URL}/api/v1/quotes`, payload, {
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
      'Idempotency-Key': idempotencyKey,
    },
  });

  check(res, {
    'status is 201': (r) => r.status === 201,
  });

  sleep(0.2);
}
