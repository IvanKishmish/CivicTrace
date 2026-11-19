import { exportRentalsUrl, fetchRentalAnalytics, fetchRentalMap, fetchRentals } from './api.js';
import { coordStatusText, formatArea, formatMoney, listTypeLabel, safeUrl, statusBadgeClass } from './format.js';
import { renderMapWidget } from './map-widget.js';
import { escapeHtml, renderLastUpdatedBanner, renderNav } from './nav.js';

renderNav('catalog');

const form = document.getElementById('filters');
const fieldset = form.querySelector('.catalog-controls');
const searchEl = document.getElementById('search');
const statusEl = document.getElementById('status');
const listTypeEl = document.getElementById('list-type');
const objTypeEl = document.getElementById('object-type');
const districtEl = document.getElementById('district');
const yearEl = document.getElementById('year');
const minAreaEl = document.getElementById('min-area');
const maxAreaEl = document.getElementById('max-area');
const sortEl = document.getElementById('sort-by');
const unenrichedEl = document.getElementById('only-unenriched');
const defectsEl = document.getElementById('only-defects');

const resultsEl = document.getElementById('results');
const countEl = document.getElementById('result-count');
const paginationEl = document.getElementById('pagination');
const kpiEl = document.getElementById('kpi-strip');
const bannerEl = document.getElementById('catalog-mode');
const miniMapEl = document.getElementById('catalog-map');
const mapSummaryEl = document.getElementById('catalog-map-summary');
const refreshMapBtn = document.getElementById('refresh-map');

let page = 1;
let timer = null;
let mapHandle = null;
let mapRetryTimer = null;
let mapRetryCount = 0;
const mapRetryLimit = 8;

function params() {
  return {
    search: searchEl.value.trim(),
    status: statusEl.value,
    listType: listTypeEl.value,
    objectType: objTypeEl.value,
    district: districtEl.value,
    year: yearEl.value,
    minArea: minAreaEl.value,
    maxArea: maxAreaEl.value,
    sortBy: sortEl.value,
    onlyUnenriched: unenrichedEl.checked,
    onlyWithDefects: defectsEl.checked,
    pageNumber: page,
    pageSize: 15
  };
}

function renderCards(items) {
  if (!items.length) {
    resultsEl.innerHTML = '<div class="panel empty-state"><h3>Об’єктів не знайдено</h3><p>Змініть фільтри або скиньте пошук.</p></div>';
    return;
  }

  resultsEl.innerHTML = items.map((r) => {
    const url = safeUrl(r.canonicalUrl);
    const enrichedBadge = r.enrichmentStatus === 'Enriched'
      ? '<span class="badge badge-info">Збагачено з API</span>'
      : '<span class="badge badge-warn">Очікує збагачення</span>';

    return `
      <article class="panel procurement-card">
        <div class="card-top">
          <span class="eyebrow">${escapeHtml(r.objectType)} · ${escapeHtml(listTypeLabel(r.listType))}</span>
          <div class="card-badges">
            <span class="${statusBadgeClass(r.statusCode)}">${escapeHtml(r.statusLabel)}</span>
            ${enrichedBadge}
            ${r.riskScore > 0 ? `<span class="badge badge-risk">Ознак ризику: ${r.riskFlags.length} (${r.riskScore})</span>` : ''}
          </div>
        </div>

        <h3><a href="./rental.html?id=${encodeURIComponent(r.ocid)}">${escapeHtml(r.title)}</a></h3>
        <div class="record-id">${escapeHtml(r.ocid)}${r.createdYear ? ` · ${r.createdYear} р.` : ''}</div>

        <div class="card-facts">
          <div>
            <span class="fact-label">Площа</span>
            <strong>${escapeHtml(formatArea(r.areaSqm))}</strong>
          </div>
          <div>
            <span class="fact-label">Адреса</span>
            <strong>${escapeHtml(r.address || 'Ще не збагачено')}</strong>
          </div>
          <div>
            <span class="fact-label">Балансоутримувач / Договір</span>
            <strong>${escapeHtml(r.propertyOwnerName || (r.currentContractValue != null ? formatMoney(r.currentContractValue) : 'Не вказано'))}</strong>
          </div>
        </div>

        <div class="small-note" style="margin-bottom:10px;">
          ${escapeHtml(coordStatusText(r.coordinatesStatus, r.hasCoordinates))}
          ${r.auctions.length ? ` · Аукціонів: <strong>${r.auctions.length}</strong>` : ''}
          ${r.dataDefectsNotes ? ` · <span style="color:var(--amber);">Дефект набору P3-01: ${escapeHtml(r.dataDefectsNotes)}</span>` : ''}
        </div>

        <div class="card-bottom">
          <a class="button button-secondary button-sm" href="./rental.html?id=${encodeURIComponent(r.ocid)}">Картка об’єкта →</a>
          ${url ? `<a href="${escapeHtml(url)}" target="_blank" rel="noopener noreferrer">Prozorro.Продажі ↗</a>` : ''}
        </div>
      </article>
    `;
  }).join('');
}

function renderPagination(data) {
  if (data.totalPages <= 1) { paginationEl.innerHTML = ''; return; }
  paginationEl.innerHTML = `
    <span class="muted">Сторінка ${data.pageNumber} з ${data.totalPages} (усього ${data.totalCount})</span>
    <div style="display:flex;gap:8px;">
      <button type="button" class="button button-secondary button-sm" id="prev-p" ${data.pageNumber <= 1 ? 'disabled' : ''}>← Назад</button>
      <button type="button" class="button button-secondary button-sm" id="next-p" ${data.pageNumber >= data.totalPages ? 'disabled' : ''}>Далі →</button>
    </div>
  `;
  document.getElementById('prev-p')?.addEventListener('click', () => { page = Math.max(1, page - 1); refresh(); });
  document.getElementById('next-p')?.addEventListener('click', () => { page = Math.min(data.totalPages, page + 1); refresh(); });
}

async function refresh() {
  resultsEl.setAttribute('aria-busy', 'true');
  try {
    const data = await fetchRentals(params());
    countEl.textContent = `Знайдено об’єктів: ${data.totalCount}`;
    renderCards(data.items);
    renderPagination(data);
  } catch (err) {
    resultsEl.innerHTML = `<div class="panel empty-state"><p>${escapeHtml(err.message)}</p></div>`;
  } finally {
    resultsEl.setAttribute('aria-busy', 'false');
  }
}

async function refreshMap({ automatic = false } = {}) {
  if (!refreshMapBtn || !miniMapEl) return;
  if (!automatic) {
    mapRetryCount = 0;
    clearTimeout(mapRetryTimer);
    mapSummaryEl.textContent = 'Оновлюємо карту…';
  }

  refreshMapBtn.disabled = true;
  try {
    const mapData = await fetchRentalMap();
    const waitingForCoordinates = mapData.totalObjects > 0 && mapData.mappedCount === 0;
    mapSummaryEl.textContent = waitingForCoordinates
      ? `Об’єкти завантажено (${mapData.totalObjects}), координати ще синхронізуються. Карта оновиться автоматично.`
      : `На мапі: ${mapData.mappedCount} із ${mapData.totalObjects} об’єктів · Негеокодовано: ${mapData.ungeocodedPercent}%`;

    mapHandle?.destroy();
    mapHandle = renderMapWidget(miniMapEl, mapData.points, { scrollWheelZoom: false });

    if (waitingForCoordinates && mapRetryCount < mapRetryLimit) {
      mapRetryCount += 1;
      mapRetryTimer = setTimeout(() => refreshMap({ automatic: true }), 15000);
    } else if (!waitingForCoordinates) {
      mapRetryCount = 0;
    } else {
      mapSummaryEl.textContent += ' Натисніть «Оновити», щоб перевірити ще раз.';
    }
  } catch (err) {
    mapSummaryEl.textContent = `Не вдалося завантажити карту: ${err.message}. Перевірте з’єднання з API та спробуйте ще раз.`;
    mapHandle?.destroy();
    mapHandle = null;
    if (mapRetryCount < mapRetryLimit) {
      mapRetryCount += 1;
      mapRetryTimer = setTimeout(() => refreshMap({ automatic: true }), 15000);
    }
  } finally {
    refreshMapBtn.disabled = false;
  }
}

async function initSummary() {
  try {
    const analytics = await fetchRentalAnalytics();
    renderLastUpdatedBanner(bannerEl, analytics.lastUpdatedAt, `Збагачено ${analytics.enrichedCount} із ${analytics.totalObjects}`);

    const freeCount = analytics.byStatus.find((s) => s.key === 'free')?.count ?? 0;
    const rentedCount = analytics.byStatus.find((s) => s.key === 'rented')?.count ?? 0;

    kpiEl.innerHTML = `
      <div class="panel kpi-card"><span>Об’єктів у переліку P3-01</span><strong>${analytics.totalObjects}</strong></div>
      <div class="panel kpi-card"><span>Збагачено з Prozorro.Продажі</span><strong>${analytics.enrichedCount}</strong></div>
      <div class="panel kpi-card"><span>Вільні / В оренді</span><strong>${freeCount} / ${rentedCount}</strong></div>
      <div class="panel kpi-card"><span>Загальна відома площа</span><strong>${escapeHtml(formatArea(analytics.totalAreaSqm))}</strong></div>
    `;

    for (const t of analytics.byObjectType) {
      const o = document.createElement('option');
      o.value = t.key;
      o.textContent = `${t.label} (${t.count})`;
      objTypeEl.append(o);
    }
    for (const y of analytics.byYear) {
      if (!/^\d{4}$/.test(y.key)) continue;
      const o = document.createElement('option');
      o.value = y.key;
      o.textContent = `${y.key} (${y.count})`;
      yearEl.append(o);
    }
  } catch (err) {
    kpiEl.innerHTML = `<div class="panel empty-state" role="status">Не вдалося завантажити показники: ${escapeHtml(err.message)}</div>`;
    bannerEl.textContent = 'Аналітика тимчасово недоступна. Перевірте підключення до API.';
  }
}

refreshMapBtn?.addEventListener('click', () => refreshMap());

document.getElementById('export-csv')?.addEventListener('click', () => { location.href = exportRentalsUrl('csv'); });
document.getElementById('export-json')?.addEventListener('click', () => { location.href = exportRentalsUrl('json'); });

searchEl.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(() => { page = 1; refresh(); }, 220); });
[statusEl, listTypeEl, objTypeEl, districtEl, yearEl, minAreaEl, maxAreaEl, sortEl, unenrichedEl, defectsEl].forEach((el) => {
  el.addEventListener('change', () => { page = 1; refresh(); });
});
form.addEventListener('reset', () => { setTimeout(() => { page = 1; refresh(); }, 0); });
form.addEventListener('submit', (e) => { e.preventDefault(); page = 1; refresh(); });

fieldset.disabled = false;
initSummary();
refreshMap();
refresh();