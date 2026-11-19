import { getAuthState, loginAdmin, logoutAdmin, triggerRentalSync, tryRefreshSession } from './api.js';
import { escapeHtml, renderNav } from './nav.js';

renderNav('admin');

const authStatusEl = document.getElementById('auth-status');
const authForm = document.getElementById('auth-form');
const logoutBtn = document.getElementById('logout-btn');
const authMsg = document.getElementById('auth-message');
const adminPanel = document.getElementById('admin-workspace');
const syncBtn = document.getElementById('sync-btn');
const syncStatus = document.getElementById('sync-status');

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

syncBtn.addEventListener('click', async () => {
  syncBtn.disabled = true;
  syncStatus.innerHTML = '<p class="muted">Збагачуємо 553 об’єкти оренди та аукціони з Prozorro.Продажі API… Це може тривати 30–90 секунд.</p>';
  try {
    const res = await triggerRentalSync(60);
    syncStatus.innerHTML = `
      <div class="notice notice-ok">
        <strong>Синхронізацію завершено!</strong><br>
        Збагачено в цьому запуску: <strong>${res.objectsEnriched}</strong> ·
        Нових аукціонів: <strong>${res.auctionsImported}</strong> ·
        Геокодовано адрес: <strong>${res.geocodedCount}</strong><br>
        Усього збагачено в базі: <strong>${res.totalEnrichedInDb}</strong> ·
        Ще не збагачено: <strong>${res.totalNotEnrichedInDb}</strong>
      </div>
    `;
  } catch (err) {
    syncStatus.innerHTML = `<div class="notice notice-risk">${escapeHtml(err.message)}</div>`;
  } finally {
    syncBtn.disabled = false;
  }
});

tryRefreshSession().then(updateUi);