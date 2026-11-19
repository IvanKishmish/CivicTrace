import { getAuthState, tryRefreshSession } from './api.js';

export function renderNav(activeKey) {
  const nav = document.getElementById('site-nav');
  if (!nav) return;
  const links = [
    { key: 'catalog', href: './index.html', label: 'Каталог' },
    { key: 'map', href: './map.html', label: 'Мапа об’єктів' },
    { key: 'analytics', href: './analytics.html', label: 'Аналітика і ризики' },
    { key: 'about', href: './about.html', label: 'Про сервіс' },
    { key: 'admin', href: './import.html', label: 'Вхід' }
  ];
  nav.innerHTML = links
    .map((l) => `<a class="nav-link${l.key === activeKey ? ' is-active' : ''}${l.key === 'admin' ? ' nav-link-admin' : ''}" href="${l.href}"${l.key === activeKey ? ' aria-current="page"' : ''}>${l.key === 'admin' ? (getAuthState().roles.includes('Admin') ? 'Кабінет адміністратора' : 'Вхід адміністратора') : l.label}</a>`)
    .join('');
  const toggle = document.createElement('button');
  toggle.type = 'button';
  toggle.className = 'theme-toggle';
  toggle.setAttribute('role', 'switch');
  toggle.setAttribute('aria-label', 'Темна тема');
  toggle.innerHTML = '<span class="theme-track" aria-hidden="true"><span class="theme-thumb"></span><span class="theme-sun">☀</span><span class="theme-moon">☾</span></span>';
  function updateThemeLabel() {
    const dark = document.documentElement.dataset.theme === 'dark';
    toggle.setAttribute('aria-checked', String(dark));
    toggle.title = `Увімкнути ${dark ? 'світлу' : 'темну'} тему`;
  }
  updateThemeLabel();
  toggle.addEventListener('click', () => {
    const theme = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
    document.documentElement.dataset.theme = theme;
    try { localStorage.setItem('civictrace-theme', theme); } catch {}
    updateThemeLabel();
  });
  nav.append(toggle);
  window.addEventListener('civictrace-auth', () => {
    nav.querySelector('.nav-link-admin').textContent = getAuthState().roles.includes('Admin') ? 'Кабінет адміністратора' : 'Вхід адміністратора';
  });
  if (activeKey !== 'admin') tryRefreshSession();
}

export function renderLastUpdatedBanner(el, lastUpdatedAt, extraText = '') {
  if (!el) return;
  const dateText = lastUpdatedAt
    ? new Date(lastUpdatedAt).toLocaleString('uk-UA')
    : 'Стартовий набір P3-01 (очікує збагачення)';
  el.innerHTML = `
    <span class="status-pill">
      <span class="live-dot" aria-hidden="true"></span>
      <span>Дані Полтави: набір P3-01 + Prozorro.Продажі</span>
    </span>
    <span class="small-note">Останнє оновлення: <strong>${escapeHtml(dateText)}</strong>${extraText ? ` · ${escapeHtml(extraText)}` : ''}</span>
  `;
}

export function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}