export function renderNav(activeKey) {
  const nav = document.getElementById('site-nav');
  if (!nav) return;
  const links = [
    { key: 'catalog', href: './index.html', label: 'Каталог (553)' },
    { key: 'map', href: './map.html', label: 'Мапа об’єктів' },
    { key: 'analytics', href: './analytics.html', label: 'Аналітика і ризики' },
    { key: 'about', href: './about.html', label: 'Методика і дефекти' }
  ];
  nav.innerHTML = links
    .map((l) => `<a class="nav-link${l.key === activeKey ? ' is-active' : ''}" href="${l.href}"${l.key === activeKey ? ' aria-current="page"' : ''}>${l.label}</a>`)
    .join('');
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