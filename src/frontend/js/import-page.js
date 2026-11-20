import { fetchRentalSyncStatus, getAuthState, loginAdmin, logoutAdmin, triggerRentalSync, tryRefreshSession } from './api.js';
import { escapeHtml, renderNav } from './nav.js';

renderNav('admin');

const authStatusEl = document.getElementById('auth-status');
const authForm = document.getElementById('auth-form');
const logoutBtn = document.getElementById('logout-btn');
const authMsg = document.getElementById('auth-message');
const adminPanel = document.getElementById('admin-workspace');
const syncBtn = document.getElementById('sync-btn');
const syncStatus = document.getElementById('sync-status');
const savedSyncJobKey = 'civictrace-admin-sync-job';

function readField(object, ...names) {
  if (!object || typeof object !== 'object') return undefined;
  for (const name of names) if (object[name] !== undefined) return object[name];
  return undefined;
}

function normalizeSyncResult(payload) {
  const candidates = [readField(payload, 'result', 'Result'), readField(payload, 'data', 'Data'), payload];
  for (const candidate of candidates) {
    if (!candidate || typeof candidate !== 'object') continue;
    const readNumber = (...names) => {
      const value = readField(candidate, ...names);
      return value === undefined || value === null || value === '' || !Number.isFinite(Number(value)) ? null : Number(value);
    };
    const result = {
      objectsEnriched: readNumber('objectsEnriched', 'ObjectsEnriched'),
      auctionsImported: readNumber('auctionsImported', 'AuctionsImported'),
      geocodedCount: readNumber('geocodedCount', 'GeocodedCount'),
      totalEnrichedInDb: readNumber('totalEnrichedInDb', 'TotalEnrichedInDb'),
      totalNotEnrichedInDb: readNumber('totalNotEnrichedInDb', 'TotalNotEnrichedInDb')
    };
    if (Object.values(result).every((value) => value !== null)) return result;
  }
  return null;
}

function renderSyncResult(result) {
  syncStatus.innerHTML = `<div class="notice notice-ok"><strong>Джерело відповіло, синхронізацію завершено.</strong><br>Оновлено записів: <strong>${result.objectsEnriched}</strong> · Пов’язаних аукціонів: <strong>${result.auctionsImported}</strong> · Знайдено координат: <strong>${result.geocodedCount}</strong><br>Зіставлено з API загалом: <strong>${result.totalEnrichedInDb}</strong> · Без зіставлення: <strong>${result.totalNotEnrichedInDb}</strong><br><span class="small-note">Кількість записів у міському списку від цього не змінюється.</span></div>`;
}

function updateUi() {
  const auth = getAuthState();
  const isAdmin = auth.isAuthenticated && auth.roles.includes('Admin');
  if (!auth.isAuthenticated) {
    authStatusEl.innerHTML = '<span class="badge badge-neutral">Не авторизовано</span>';
    logoutBtn.hidden = true;
    authForm.hidden = false;
    adminPanel.hidden = true;
    return;
  }
  authStatusEl.innerHTML = `<span class="badge">${escapeHtml(auth.email || auth.userId)}</span> <span class="badge badge-info">${isAdmin ? 'Admin' : 'Недостатньо прав'}</span>`;
  logoutBtn.hidden = false;
  authForm.hidden = true;
  adminPanel.hidden = !isAdmin;
}

authForm.addEventListener('submit', async (e) => {
  e.preventDefault();
  authMsg.textContent = 'Виконуємо вхід…';
  try {
    await loginAdmin(document.getElementById('login-email').value.trim(), document.getElementById('login-password').value);
    authMsg.textContent = '';
    updateUi();
  } catch (err) {
    authMsg.textContent = `Помилка: ${err.message}`;
  }
});

logoutBtn.addEventListener('click', async () => { await logoutAdmin(); updateUi(); });

async function watchSyncJob(jobId) {
  syncBtn.disabled = true;
  syncBtn.textContent = 'Синхронізація триває…';
  try {
    let rawStatus = await fetchRentalSyncStatus(jobId);
    let state = String(readField(rawStatus, 'status', 'Status') || '').toLowerCase();
    while (state === 'queued' || state === 'running') {
      const createdAt = readField(rawStatus, 'createdAt', 'CreatedAt');
      const message = readField(rawStatus, 'message', 'Message');
      const elapsed = createdAt ? Math.floor((Date.now() - new Date(createdAt).getTime()) / 1000) : null;
      syncStatus.innerHTML = `<div class="notice notice-info"><strong>${state === 'queued' ? 'У черзі' : 'Синхронізація виконується'}</strong><br>${escapeHtml(message || '')}${elapsed === null ? '' : `<br>Минуло ${elapsed} с.`} Можна перейти на іншу сторінку й повернутися сюди, щоб перевірити стан.</div>`;
      await new Promise((resolve) => setTimeout(resolve, 3000));
      rawStatus = await fetchRentalSyncStatus(jobId);
      state = String(readField(rawStatus, 'status', 'Status') || '').toLowerCase();
    }
    const result = normalizeSyncResult(rawStatus);
    if (state === 'completed' && result) {
      renderSyncResult(result);
    } else if (state === 'completed') {
      syncStatus.innerHTML = '<div class="notice notice-risk"><strong>Сервер повідомив про завершення, але не надіслав підсумкові числа.</strong><br>Не вважаємо це підтвердженням оновлення. Перезавантажте панель і перевірте час оновлення та кількість зіставлених записів.</div>';
    } else {
      syncStatus.innerHTML = `<div class="notice notice-risk">${escapeHtml(readField(rawStatus, 'message', 'Message') || 'Синхронізація завершилася помилкою.')}</div>`;
    }
    sessionStorage.removeItem(savedSyncJobKey);
  } catch (err) {
    sessionStorage.removeItem(savedSyncJobKey);
    syncStatus.innerHTML = `<div class="notice notice-risk">Не вдалося отримати стан синхронізації: ${escapeHtml(err.message)}.</div>`;
  } finally {
    syncBtn.disabled = false;
    syncBtn.textContent = 'Запустити синхронізацію з Prozorro.Продажі API';
  }
}

syncBtn.addEventListener('click', async () => {
  syncBtn.disabled = true;
  syncStatus.textContent = 'Створюємо завдання синхронізації…';
  try {
    const job = await triggerRentalSync();
    const immediateResult = normalizeSyncResult(job);
    if (immediateResult) {
      renderSyncResult(immediateResult);
      syncBtn.disabled = false;
    } else {
      const jobId = readField(job, 'jobId', 'JobId');
      if (!jobId) throw new Error(`Сервер повернув відповідь неочікуваного формату (${Object.keys(job || {}).join(', ') || 'без полів'}). Оновлення не підтверджено.`);
      sessionStorage.setItem(savedSyncJobKey, jobId);
      await watchSyncJob(jobId);
    }
  } catch (err) {
    syncStatus.innerHTML = `<div class="notice notice-risk">${escapeHtml(err.message)}</div>`;
    syncBtn.disabled = false;
  }
});

tryRefreshSession().then(() => {
  updateUi();
  if (!adminPanel.hidden) {
    const jobId = sessionStorage.getItem(savedSyncJobKey);
    if (jobId) watchSyncJob(jobId);
  }
});