import { CONFIG } from './config.js';

let authState = { accessToken: null, email: null, userId: null, roles: [] };
const SESSION_HINT_KEY = 'civictrace-admin-session';

export function getAuthState() {
  return { ...authState, isAuthenticated: Boolean(authState.accessToken) };
}

export function clearAuthState() {
  authState = { accessToken: null, email: null, userId: null, roles: [] };
  try { localStorage.removeItem(SESSION_HINT_KEY); } catch {}
  window.dispatchEvent(new Event('civictrace-auth'));
}

function decodeJwtPayload(token) {
  try {
    const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const json = decodeURIComponent(atob(base64).split('').map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join(''));
    return JSON.parse(json);
  } catch { return {}; }
}

function setAccessToken(token) {
  const payload = decodeJwtPayload(token);
  const rawRoles = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? payload.role ?? [];
  const roles = Array.isArray(rawRoles) ? rawRoles : rawRoles ? [rawRoles] : [];
  try { localStorage.setItem(SESSION_HINT_KEY, '1'); } catch {}
  authState = { accessToken: token, email: payload.email ?? null, userId: payload.sub ?? null, roles };
  window.dispatchEvent(new Event("civictrace-auth"));
  return getAuthState();
}

async function parseError(res) {
  const text = await res.text().catch(() => '');
  try {
    const d = JSON.parse(text);
    return d.detail || d.title || `HTTP ${res.status}`;
  } catch { return text || `HTTP ${res.status}`; }
}

export async function fetchRentals(params = {}) {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v !== null && v !== undefined && v !== '' && v !== false) qs.set(k, String(v));
  }
  const res = await fetch(`${CONFIG.apiBaseUrl}/rentals?${qs.toString()}`);
  if (!res.ok) throw new Error(await parseError(res));
  return await res.json();
}

export async function fetchRentalByOcid(ocid) {
  const res = await fetch(`${CONFIG.apiBaseUrl}/rentals/${encodeURIComponent(ocid)}`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(await parseError(res));
  return await res.json();
}

export async function fetchRentalMap() {
  const res = await fetch(`${CONFIG.apiBaseUrl}/rentals/map`);
  if (!res.ok) throw new Error(await parseError(res));
  return await res.json();
}

export async function fetchRentalAnalytics() {
  const res = await fetch(`${CONFIG.apiBaseUrl}/rentals/analytics`);
  if (!res.ok) throw new Error(await parseError(res));
  return await res.json();
}

export function exportRentalsUrl(format = 'csv') {
  return `${CONFIG.apiBaseUrl}/rentals/export?format=${encodeURIComponent(format)}`;
}

export async function loginAdmin(email, password) {
  const res = await fetch(`${CONFIG.apiBaseUrl}/auth/login`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password })
  });
  if (!res.ok) throw new Error(await parseError(res));
  const data = await res.json();
  return setAccessToken(data.accessToken);
}

export async function tryRefreshSession() {
  try {
    if (localStorage.getItem(SESSION_HINT_KEY) !== '1') return null;
    const res = await fetch(`${CONFIG.apiBaseUrl}/auth/refresh`, { method: 'POST', credentials: 'include' });
    if (res.status === 401 || res.status === 403) localStorage.removeItem(SESSION_HINT_KEY);
    if (!res.ok) return null;
    const data = await res.json();
    return setAccessToken(data.accessToken);
  } catch { return null; }
}

export async function logoutAdmin() {
  try {
    if (authState.accessToken) {
      await fetch(`${CONFIG.apiBaseUrl}/auth/logout`, {
        method: 'POST',
        credentials: 'include',
        headers: { Authorization: `Bearer ${authState.accessToken}` }
      });
    }
  } finally { clearAuthState(); }
}

export async function triggerRentalSync(maxBatchesPerSlice = 60, signal) {
  if (!authState.accessToken) throw new Error('Потрібен вхід адміністратора.');
  const res = await fetch(`${CONFIG.apiBaseUrl}/rentals/sync`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${authState.accessToken}` },
    body: JSON.stringify({ maxBatchesPerSlice }),
    signal
  });
  if (!res.ok) throw new Error(await parseError(res));
  return await res.json();
}