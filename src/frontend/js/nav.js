export function renderNav(activeKey) {
  const nav = document.getElementById('site-nav');
  if (!nav) return;

  const links = [
    { key: 'catalog', href: './index.html', label: 'Каталог' },
    { key: 'map', href: './map.html', label: 'Мапа' },
    { key: 'analytics', href: './analytics.html', label: 'Аналітика' },
    { key: 'about', href: './about.html', label: 'Про дані' }
  ];

  nav.innerHTML = links
    .map(
      (l) =>
        `<a class="nav-link${l.key === activeKey ? ' is-active' : ''}" href="${l.href}"${
          l.key === activeKey ? ' aria-current="page"' : ''
        }>${l.label}</a>`
    )
    .join('');
}

export function renderSourceBanner(el, sourceInfo) {
  if (!el) return;
  const isApi = sourceInfo.mode === 'api';
  const dotClass = isApi ? 'live-dot' : 'live-dot is-fallback';
  const label = isApi
    ? 'Дані сервісу: база закупівель (Prozorro)'
    : 'Автономний режим: локальна копія даних';

  el.innerHTML = `
    <span class="status-pill">
      <span class="${dotClass}" aria-hidden="true"></span>
      <span>${label}</span>
    </span>
    <span class="small-note">Дані відображаються як у джерелі; відсутність записів не є доказом порушення чи виконання.</span>
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