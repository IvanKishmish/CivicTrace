import {
  getAuthState,
  getSourceInfo,
  importProcurementFromProzorro,
  loginUser,
  logoutUser,
  syncPoltavaFromProzorro,
  tryRefreshSession,
  verifyProcurement
} from './api.js';
import { renderProcurementDetailHtml } from './detail.js';
import { renderMapWidget } from './map-widget.js';
import { escapeHtml, renderNav, renderSourceBanner } from './nav.js';

renderNav('admin');

const bannerEl = document.getElementById('import-mode');
const authStatusEl = document.getElementById('auth-status');
const authForm = document.getElementById('auth-form');
const logoutBtn = document.getElementById('logout-btn');
const authMessageEl = document.getElementById('auth-message');
const adminWorkspace = document.getElementById('admin-workspace');

const syncBtn = document.getElementById('sync-poltava-btn');
const syncStatusEl = document.getElementById('sync-status');

const importForm = document.getElementById('import-form');
const apiIdInput = document.getElementById('api-id');
const dryRunCheck = document.getElementById('dry-run');
const replaceCheck = document.getElementById('replace-existing');
const importStatusEl = document.getElementById('import-status');
const importPreviewEl = document.getElementById('import-preview');

const verifyForm = document.getElementById('verify-form');
const verifyIdInput = document.getElementById('verify-external-id');
const verifyDateInput = document.getElementById('verify-date');
const verifyStatusEl = document.getElementById('verify-status');

function updateAuthUi() {
  const auth = getAuthState();
  const isAdmin = auth.isAuthenticated && auth.roles.includes('Admin');

  if (!auth.isAuthenticated) {
    authStatusEl.innerHTML =
      '<span class="badge badge-neutral">Не авторизовано</span> Доступ лише для адміністратора.';
    logoutBtn.hidden = true;
    authForm.hidden = false;
    adminWorkspace.hidden = true;
    return;
  }

  authStatusEl.innerHTML = `
    <span class="badge">Увійшли: ${escapeHtml(auth.email || auth.userId)}</span>
    <span class="badge ${isAdmin ? 'badge-info' : 'badge-warn'}">${
      isAdmin ? 'Роль: Admin' : 'Недостатньо прав (потрібен Admin)'
    }</span>
  `;
  logoutBtn.hidden = false;
  authForm.hidden = true;
  adminWorkspace.hidden = !isAdmin;

  if (!isAdmin) {
    authMessageEl.textContent = 'Цей акаунт не має ролі Admin.';
  } else {
    authMessageEl.textContent = '';
  }
}

authForm.addEventListener('submit', async (e) => {
  e.preventDefault();
  authMessageEl.textContent = 'Виконуємо вхід…';
  try {
    await loginUser(
      document.getElementById('login-email').value.trim(),
      document.getElementById('login-password').value
    );
    authMessageEl.textContent = '';
    updateAuthUi();
  } catch (err) {
    authMessageEl.textContent = `Помилка входу: ${err.message}`;
  }
});

logoutBtn.addEventListener('click', async () => {
  await logoutUser();
  authMessageEl.textContent = 'Вийшли з облікового запису.';
  importStatusEl.innerHTML = '';
  importPreviewEl.innerHTML = '';
  syncStatusEl.innerHTML = '';
  updateAuthUi();
});

syncBtn?.addEventListener('click', async () => {
  syncBtn.disabled = true;
  syncStatusEl.innerHTML =
    '<p class="muted">Скануємо стрічку Prozorro API по Полтаві та геокодуємо адреси… Це може тривати 15–40 секунд.</p>';
  try {
    const res = await syncPoltavaFromProzorro({ maxPages: 20, targetNewCount: 25 });
    syncStatusEl.innerHTML = `
      <div class="notice notice-ok">
        <strong>Синхронізацію завершено!</strong><br>
        Переглянуто сторінок стрічки Prozorro: <strong>${res.pagesScanned}</strong> ·
        Знайдено по Полтаві: <strong>${res.matchedPoltava}</strong> ·
        Імпортовано нових: <strong>${res.imported}</strong> ·
        Геокодовано наявних адрес: <strong>${res.geocodedExisting}</strong> ·
        Вже були в базі: ${res.skippedExisting} ·
        Пропущено (інший формат): ${res.failed}
        <div style="margin-top:8px;">
          <a class="button button-sm" href="./index.html">Перейти в каталог →</a>
          <a class="button button-secondary button-sm" href="./map.html">Перевірити на мапі →</a>
        </div>
      </div>
    `;
  } catch (err) {
    syncStatusEl.innerHTML = `<div class="notice"><strong>Помилка синхронізації:</strong> ${escapeHtml(
      err.message
    )}</div>`;
  } finally {
    syncBtn.disabled = false;
  }
});

document.querySelectorAll('[data-preset-api-id]').forEach((btn) => {
  btn.addEventListener('click', () => {
    apiIdInput.value = btn.getAttribute('data-preset-api-id');
  });
});

importForm.addEventListener('submit', async (e) => {
  e.preventDefault();
  importStatusEl.innerHTML = '<p class="muted">Запитуємо публічний API Prozorro…</p>';
  importPreviewEl.innerHTML = '';

  try {
    const res = await importProcurementFromProzorro({
      apiId: apiIdInput.value.trim(),
      dryRun: dryRunCheck.checked,
      replaceExisting: replaceCheck.checked
    });

    const warningsHtml = res.warnings?.length
      ? `<div class="notice"><strong>Попередження:</strong><ul class="clean-list">${res.warnings
          .map((w) => `<li>${escapeHtml(w)}</li>`)
          .join('')}</ul></div>`
      : '';

    importStatusEl.innerHTML = `
      <div class="notice ${res.saved ? 'notice-ok' : 'notice-info'}">
        <strong>${
          res.saved
            ? 'Закупівлю збережено в базу даних!'
            : 'Режим перевірки (dryRun): у базу нічого не записано.'
        }</strong>
        ${
          res.saved
            ? `<div style="margin-top:8px;"><a class="button button-sm" href="./procurement.html?id=${encodeURIComponent(
                res.procurement.id
              )}">Відкрити картку закупівлі →</a></div>`
            : ''
        }
      </div>
      ${warningsHtml}
    `;

    importPreviewEl.innerHTML = renderProcurementDetailHtml(res.procurement, {
      showVerifyAction: false
    });
    const mapBox = document.getElementById('detail-map');
    if (mapBox) {
      renderMapWidget(mapBox, res.procurement.hasCoordinates ? [res.procurement] : [], {
        zoom: 15,
        openSinglePopup: true
      });
    }
  } catch (err) {
    importStatusEl.innerHTML = `<div class="notice"><strong>Помилка імпорту:</strong> ${escapeHtml(
      err.message
    )}</div>`;
  }
});

verifyForm.addEventListener('submit', async (e) => {
  e.preventDefault();
  verifyStatusEl.textContent = 'Оновлюємо статус перевірки…';
  try {
    await verifyProcurement(verifyIdInput.value.trim(), verifyDateInput.value || null);
    verifyStatusEl.innerHTML = `Позначено перевіреною. <a href="./procurement.html?id=${encodeURIComponent(
      verifyIdInput.value.trim()
    )}">Відкрити картку →</a>`;
  } catch (err) {
    verifyStatusEl.textContent = `Помилка: ${err.message}`;
  }
});

async function init() {
  renderSourceBanner(bannerEl, getSourceInfo());
  await tryRefreshSession();
  updateAuthUi();
}

init();