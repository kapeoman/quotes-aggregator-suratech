import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 1,
  duration: '30s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<800'],
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

  const payload = JSON.stringify({
    documentId: `DOC-SMOKE-${__VU}`,
    amount: 123.45,
    currency: 'CLP',
  });

  const idempotencyKey = `smoke-${__VU}-${__ITER}-${Date.now()}`;

  const res = http.post(`${BASE_URL}/api/v1/quotes`, payload, {
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
      'Idempotency-Key': idempotencyKey,
    },
  });

  check(res, {
    'status is 201': (r) => r.status === 201,
    'has id': (r) => !!r.json('id'),
  });

  sleep(1);
}
