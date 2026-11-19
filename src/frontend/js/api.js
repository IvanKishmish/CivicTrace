import { CONFIG } from './config.js';
import { normalizeMapPoint, normalizeProcurement } from './mapper.js';
import { firstEventYear, kindCodeFromLabel, methodCodeFromLabel } from './format.js';

let seedCachePromise = null;
let lastSourceMode = { mode: 'api', apiBase: CONFIG.apiBaseUrl };

let authState = {
  accessToken: null,
  email: null,
  userId: null,
  roles: []
};

export function getSourceInfo() {
  return lastSourceMode;
}

export function getAuthState() {
  return { ...authState, isAuthenticated: Boolean(authState.accessToken) };
}

export function clearAuthState() {
  authState = { accessToken: null, email: null, userId: null, roles: [] };
}

function decodeJwtPayload(token) {
  try {
    const part = token.split('.')[1];
    const base64 = part.replace(/-/g, '+').replace(/_/g, '/');
    const json = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    return JSON.parse(json);
  } catch {
    return {};
  }
}

function setAccessToken(token) {
  const payload = decodeJwtPayload(token);
  const rawRoles =
    payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ??
    payload.role ??
    [];
  const roles = Array.isArray(rawRoles) ? rawRoles : rawRoles ? [rawRoles] : [];

  authState = {
    accessToken: token,
    email: payload.email ?? null,
    userId: payload.sub ?? null,
    roles
  };
  return getAuthState();
}

async function fetchJsonWithTimeout(url, options = {}, timeoutMs = 8000) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(url, { ...options, signal: controller.signal });
  } finally {
    clearTimeout(timer);
  }
}

async function parseErrorMessage(res) {
  const text = await res.text().catch(() => '');
  if (!text) return `HTTP ${res.status}`;
  try {
    const data = JSON.parse(text);
    if (data.errors) {
      const list = Object.values(data.errors).flat();
      if (list.length) return list.join(' ');
    }
    return data.detail || data.title || `HTTP ${res.status}`;
  } catch {
    return text.slice(0, 240);
  }
}

export async function loadSeedRecords() {
  if (!seedCachePromise) {
    seedCachePromise = (async () => {
      for (const candidate of CONFIG.seedFallbackUrls) {
        try {
          const res = await fetchJsonWithTimeout(candidate, {}, 4000);
          if (!res.ok) continue;
          const data = await res.json();
          if (Array.isArray(data)) {
            return data.map(normalizeProcurement);
          }
        } catch {
          // пробуємо наступний шлях
        }
      }
      throw new Error('Не вдалося завантажити ані API, ані резервний сид-файл.');
    })();
  }
  return seedCachePromise;
}

export async function fetchProcurements(params = {}) {
  const qs = new URLSearchParams();
  if (params.search) qs.set('search', params.search);
  if (params.status) qs.set('status', params.status);
  if (params.method) qs.set('method', params.method);
  if (params.kind) qs.set('kind', params.kind);
  if (params.year) qs.set('year', String(params.year));
  if (params.buyerEdrpou) qs.set('buyerEdrpou', params.buyerEdrpou);
  if (params.repairOnly) qs.set('repairOnly', 'true');
  qs.set('pageNumber', String(params.pageNumber || 1));
  qs.set('pageSize', String(params.pageSize || CONFIG.pageSize));

  try {
    const res = await fetchJsonWithTimeout(`${CONFIG.apiBaseUrl}/procurements?${qs.toString()}`);
    if (!res.ok) throw new Error(await parseErrorMessage(res));
    const data = await res.json();
    lastSourceMode = { mode: 'api', apiBase: CONFIG.apiBaseUrl };
    return {
      items: (data.items || []).map(normalizeProcurement),
      pageNumber: data.pageNumber ?? 1,
      pageSize: data.pageSize ?? CONFIG.pageSize,
      totalCount: data.totalCount ?? 0,
      totalPages: data.totalPages ?? 1
    };
  } catch {
    const all = await loadSeedRecords();
    lastSourceMode = { mode: 'fallback', apiBase: CONFIG.apiBaseUrl };
    return filterSeedPaged(all, params);
  }
}

function filterSeedPaged(all, params) {
  const q = (params.search || '').trim().toLowerCase();
  let filtered = all.filter((p) => {
    if (q) {
      const hay = `${p.title} ${p.id} ${p.buyer} ${p.contractor || ''} ${p.cpvCode}`.toLowerCase();
      if (!hay.includes(q)) return false;
    }
    if (params.status && p.status !== params.status) return false;
    if (params.kind && kindCodeFromLabel(p.kind) !== params.kind) return false;
    if (params.method && methodCodeFromLabel(p.procurementType) !== params.method) return false;
    if (params.buyerEdrpou && p.buyerEdrpou !== params.buyerEdrpou) return false;
    if (params.repairOnly && !String(p.cpvCode).startsWith('45')) return false;
    if (params.year && firstEventYear(p) !== Number(params.year)) return false;
    return true;
  });

  filtered = filtered.sort((a, b) => (a.id < b.id ? 1 : a.id > b.id ? -1 : 0));

  const pageNumber = Math.max(1, Number(params.pageNumber) || 1);
  const pageSize = Math.max(1, Number(params.pageSize) || CONFIG.pageSize);
  const totalCount = filtered.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const items = filtered.slice((pageNumber - 1) * pageSize, pageNumber * pageSize);

  return { items, pageNumber, pageSize, totalCount, totalPages };
}

export async function fetchProcurementById(externalId) {
  try {
    const res = await fetchJsonWithTimeout(
      `${CONFIG.apiBaseUrl}/procurements/${encodeURIComponent(externalId)}`
    );
    if (res.status === 404) {
      lastSourceMode = { mode: 'api', apiBase: CONFIG.apiBaseUrl };
      return null;
    }
    if (!res.ok) throw new Error(await parseErrorMessage(res));
    const data = await res.json();
    lastSourceMode = { mode: 'api', apiBase: CONFIG.apiBaseUrl };
    return normalizeProcurement(data);
  } catch {
    const all = await loadSeedRecords();
    lastSourceMode = { mode: 'fallback', apiBase: CONFIG.apiBaseUrl };
    return all.find((p) => p.id === externalId) || null;
  }
}

export async function fetchMapData() {
  try {
    const [mapRes, listRes] = await Promise.all([
      fetchJsonWithTimeout(`${CONFIG.apiBaseUrl}/map`),
      fetchJsonWithTimeout(`${CONFIG.apiBaseUrl}/procurements?pageNumber=1&pageSize=100`)
    ]);
    if (!mapRes.ok || !listRes.ok) throw new Error('Map API error');

    const mapRaw = await mapRes.json();
    const listRaw = await listRes.json();
    lastSourceMode = { mode: 'api', apiBase: CONFIG.apiBaseUrl };

    const points = (mapRaw || []).map(normalizeMapPoint);
    const allItems = (listRaw.items || []).map(normalizeProcurement);
    const pointIds = new Set(points.map((p) => p.id));
    const unmapped = allItems.filter((p) => !pointIds.has(p.id));

    return { points, unmapped, totalCount: listRaw.totalCount ?? allItems.length };
  } catch {
    const all = await loadSeedRecords();
    lastSourceMode = { mode: 'fallback', apiBase: CONFIG.apiBaseUrl };
    const points = all
      .filter((p) => p.hasCoordinates && p.latitude !== null && p.longitude !== null)
      .map(normalizeMapPoint);
    const unmapped = all.filter((p) => !p.hasCoordinates);
    return { points, unmapped, totalCount: all.length };
  }
}

export async function fetchAnalytics(groupBy = 'buyer') {
  try {
    const res = await fetchJsonWithTimeout(
      `${CONFIG.apiBaseUrl}/analytics?groupBy=${encodeURIComponent(groupBy)}`
    );
    if (!res.ok) throw new Error(await parseErrorMessage(res));
    const data = await res.json();
    lastSourceMode = { mode: 'api', apiBase: CONFIG.apiBaseUrl };
    return data;
  } catch {
    const all = await loadSeedRecords();
    lastSourceMode = { mode: 'fallback', apiBase: CONFIG.apiBaseUrl };
    return computeAnalyticsFromSeed(all, groupBy);
  }
}

function computeAnalyticsFromSeed(records, groupBy) {
  const notes = [
    'Показники описують, що відомо з оприлюднених даних, і не оцінюють законність чи доцільність закупівель.',
    'Суми не складаються в одне число: окремо «з ПДВ», «без ПДВ» і «ПДВ не вказано».',
    'Середня кількість пропозицій рахується лише там, де сторінка закупівлі показує повний реєстр пропозицій.',
    'Закупівля без використання електронної системи — це спосіб проведення, а не висновок про порушення.',
    'Суми фінансування (fundingAmount) беруться з плану закупівлі; одна закупівля може мати кілька джерел фінансування.'
  ];

  const buildTotals = (rows, field, vatField) => {
    let withVat = 0;
    let withoutVat = 0;
    let vatUnspecified = 0;
    for (const r of rows) {
      const val = r[field];
      if (val === null || val === undefined) continue;
      const num = Number(val);
      if (r[vatField] === true) withVat += num;
      else if (r[vatField] === false) withoutVat += num;
      else vatUnspecified += num;
    }
    return { withVat, withoutVat, vatUnspecified };
  };

  const buildGroup = (key, label, rows, fundingAmount = null) => {
    const complete = rows.filter((r) => r.bidsListComplete);
    const avgBids =
      complete.length === 0
        ? null
        : complete.reduce((acc, r) => acc + r.bids.length, 0) / complete.length;
    const avgMissing =
      rows.length === 0
        ? 0
        : rows.reduce((acc, r) => acc + r.missingData.length, 0) / rows.length;

    return {
      key,
      label,
      procurementsCount: rows.length,
      contractSignedCount: rows.filter((r) => r.status === 'contract_signed').length,
      notHeldCount: rows.filter((r) => r.status === 'not_held').length,
      activeCount: rows.filter((r) => r.status === 'active').length,
      cancelledCount: rows.filter((r) => r.status === 'cancelled').length,
      noElectronicSystemCount: rows.filter(
        (r) => r.methodCode === 'no_electronic_system'
      ).length,
      bidsListCompleteCount: complete.length,
      averageBidsInCompleteLists: avgBids,
      expectedAmount: buildTotals(rows, 'expectedAmount', 'expectedVatIncluded'),
      contractAmount: buildTotals(rows, 'contractAmount', 'contractVatIncluded'),
      recordsWithMissingData: rows.filter((r) => r.missingData.length > 0).length,
      averageMissingDataItems: avgMissing,
      recordsNeedingCheck: rows.filter((r) => r.needsCheck.length > 0).length,
      fundingAmount
    };
  };

  let groups = [];

  if (groupBy === 'funding' || groupBy === 'expenditure') {
    const map = new Map();
    for (const r of records) {
      const list = r.funding || [];
      if (list.length === 0) {
        const key = 'unknown';
        const label =
          groupBy === 'funding'
            ? 'Джерело фінансування невідоме'
            : 'Код видатків невідомий';
        if (!map.has(key)) map.set(key, { label, rows: new Map(), amount: 0 });
        map.get(key).rows.set(r.id, r);
      } else {
        for (const f of list) {
          const key = groupBy === 'funding' ? f.source : f.expenditureCode || 'unknown';
          const label =
            groupBy === 'funding' ? f.source : f.expenditureCode || 'Код видатків невідомий';
          if (!map.has(key)) map.set(key, { label, rows: new Map(), amount: 0 });
          const entry = map.get(key);
          entry.rows.set(r.id, r);
          entry.amount += Number(f.amount || 0);
        }
      }
    }
    groups = Array.from(map.entries()).map(([key, v]) =>
      buildGroup(key, v.label, Array.from(v.rows.values()), v.amount)
    );
  } else {
    const map = new Map();
    const keyOf = (r) => {
      switch (groupBy) {
        case 'buyer':
          return [r.buyerEdrpou, r.buyer];
        case 'cpv':
          return [r.cpvCode, r.cpvName];
        case 'year': {
          const y = firstEventYear(r);
          return y ? [String(y), String(y)] : ['unknown', 'Рік невідомий'];
        }
        case 'method':
          return [r.methodCode || 'unknown', r.procurementType];
        case 'status':
          return [r.status, r.statusLabel];
        default:
          return [r.buyerEdrpou, r.buyer];
      }
    };
    for (const r of records) {
      const [key, label] = keyOf(r);
      if (!map.has(key)) map.set(key, { label, rows: [] });
      map.get(key).rows.push(r);
    }
    groups = Array.from(map.entries()).map(([key, v]) =>
      buildGroup(key, v.label, v.rows, null)
    );
  }

  groups.sort(
    (a, b) =>
      b.procurementsCount - a.procurementsCount ||
      (a.key < b.key ? -1 : a.key > b.key ? 1 : 0)
  );

  return {
    groupBy,
    summary: buildGroup('all', 'Усі закупівлі', records, null),
    groups,
    notes
  };
}

export async function downloadExport(format = 'csv') {
  const stamp = new Date().toISOString().slice(0, 10).replaceAll('-', '');
  const filename = `civictrace-procurements-${stamp}.${format}`;

  try {
    const res = await fetchJsonWithTimeout(
      `${CONFIG.apiBaseUrl}/export/procurements?format=${encodeURIComponent(format)}`
    );
    if (!res.ok) throw new Error('Export API failed');
    const blob = await res.blob();
    triggerBlobDownload(blob, filename);
    return { mode: 'api', filename };
  } catch {
    const records = await loadSeedRecords();
    const content =
      format === 'json'
        ? JSON.stringify(records, null, 2)
        : '\uFEFF' + buildSeedCsv(records);
    const mime =
      format === 'json'
        ? 'application/json; charset=utf-8'
        : 'text/csv; charset=utf-8';
    triggerBlobDownload(new Blob([content], { type: mime }), filename);
    return { mode: 'fallback', filename };
  }
}

function triggerBlobDownload(blob, filename) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.append(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function buildSeedCsv(records) {
  const csvText = (v) => {
    if (v === null || v === undefined || v === '') return '';
    let s = String(v);
    if ('=+-@\t\r'.includes(s[0])) s = "'" + s;
    return /[",\n\r]/.test(s) ? `"${s.replaceAll('"', '""')}"` : s;
  };
  const csvFlag = (v) => (v === null || v === undefined ? '' : v ? 'true' : 'false');
  const csvNum = (v) => (v === null || v === undefined ? '' : String(v));

  const cols = [
    ['id', (r) => csvText(r.id)],
    ['apiId', (r) => csvText(r.apiId)],
    ['planId', (r) => csvText(r.planId)],
    ['title', (r) => csvText(r.title)],
    ['kind', (r) => csvText(r.kind)],
    ['cpvCode', (r) => csvText(r.cpvCode)],
    ['cpvName', (r) => csvText(r.cpvName)],
    ['procurementType', (r) => csvText(r.procurementType)],
    ['buyer', (r) => csvText(r.buyer)],
    ['buyerEdrpou', (r) => csvText(r.buyerEdrpou)],
    ['contractor', (r) => csvText(r.contractor)],
    ['contractorEdrpou', (r) => csvText(r.contractorEdrpou)],
    ['expectedAmount', (r) => csvNum(r.expectedAmount)],
    ['expectedVatIncluded', (r) => csvFlag(r.expectedVatIncluded)],
    ['contractAmount', (r) => csvNum(r.contractAmount)],
    ['contractVatIncluded', (r) => csvFlag(r.contractVatIncluded)],
    ['currency', (r) => csvText(r.currency)],
    ['status', (r) => csvText(r.status)],
    ['sourceStatus', (r) => csvText(r.sourceStatus)],
    ['executionStatus', (r) => csvText(r.executionStatus)],
    ['address', (r) => csvText(r.address)],
    ['placeType', (r) => csvText(r.placeType)],
    ['latitude', (r) => csvNum(r.latitude)],
    ['longitude', (r) => csvNum(r.longitude)],
    ['coordinatesStatus', (r) => csvText(r.coordinatesStatus)],
    ['periodStart', (r) => csvText(r.periodStart)],
    ['periodEnd', (r) => csvText(r.periodEnd)],
    ['bidsListComplete', (r) => csvFlag(r.bidsListComplete)],
    ['bidsCount', (r) => (r.bidsListComplete ? String(r.bids.length) : '')],
    ['eventsCount', (r) => String(r.events.length)],
    [
      'firstEventDate',
      (r) => {
        const dates = r.events.map((e) => e.date).filter(Boolean).sort();
        return csvText(dates[0] || '');
      }
    ],
    ['missingDataCount', (r) => String(r.missingData.length)],
    ['needsCheckCount', (r) => String(r.needsCheck.length)],
    ['sourceUrl', (r) => csvText(r.sourceUrl)],
    ['verifiedAt', (r) => csvText(r.verifiedAt)]
  ];

  const lines = [cols.map(([h]) => h).join(',')];
  for (const r of records) {
    lines.push(cols.map(([, fn]) => fn(r)).join(','));
  }
  return lines.join('\r\n') + '\r\n';
}

export async function loginUser(email, password) {
  const res = await fetchJsonWithTimeout(`${CONFIG.apiBaseUrl}/auth/login`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password })
  });
  if (!res.ok) throw new Error(await parseErrorMessage(res));
  const data = await res.json();
  return setAccessToken(data.accessToken);
}

export async function tryRefreshSession() {
  try {
    const res = await fetchJsonWithTimeout(`${CONFIG.apiBaseUrl}/auth/refresh`, {
      method: 'POST',
      credentials: 'include'
    });
    if (!res.ok) return null;
    const data = await res.json();
    return setAccessToken(data.accessToken);
  } catch {
    return null;
  }
}

export async function logoutUser() {
  try {
    if (authState.accessToken) {
      await fetchJsonWithTimeout(`${CONFIG.apiBaseUrl}/auth/logout`, {
        method: 'POST',
        credentials: 'include',
        headers: { Authorization: `Bearer ${authState.accessToken}` }
      });
    }
  } finally {
    clearAuthState();
  }
}

export async function importProcurementFromProzorro({ apiId, dryRun, replaceExisting }) {
  if (!authState.accessToken) {
    throw new Error('Потрібен вхід адміністратора.');
  }
  const res = await fetchJsonWithTimeout(
    `${CONFIG.apiBaseUrl}/import/procurements`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${authState.accessToken}`
      },
      body: JSON.stringify({ apiId, dryRun, replaceExisting })
    },
    25000
  );
  if (!res.ok) throw new Error(await parseErrorMessage(res));
  const data = await res.json();
  return {
    ...data,
    procurement: normalizeProcurement(data.procurement)
  };
}

export async function syncPoltavaFromProzorro({ maxPages = 15, targetNewCount = 20 } = {}) {
  if (!authState.accessToken) {
    throw new Error('Потрібен вхід адміністратора.');
  }
  const res = await fetchJsonWithTimeout(
    `${CONFIG.apiBaseUrl}/import/sync`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${authState.accessToken}`
      },
      body: JSON.stringify({ maxPages, targetNewCount })
    },
    120000
  );
  if (!res.ok) throw new Error(await parseErrorMessage(res));
  return await res.json();
}

export async function verifyProcurement(externalId, verifiedAt = null) {
  if (!authState.accessToken) {
    throw new Error('Потрібен вхід адміністратора.');
  }
  const res = await fetchJsonWithTimeout(
    `${CONFIG.apiBaseUrl}/procurements/${encodeURIComponent(externalId)}/verify`,
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${authState.accessToken}`
      },
      body: JSON.stringify({ verifiedAt: verifiedAt || null })
    }
  );
  if (!res.ok) throw new Error(await parseErrorMessage(res));
  return true;
}