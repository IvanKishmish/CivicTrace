import { CONFIG } from './config.js';
import {
  downloadExport,
  fetchAnalytics,
  fetchMapData,
  fetchProcurements,
  getSourceInfo
} from './api.js';
import {
  coordinatesStatusLabel,
  executionStatusLabel,
  formatMoneyWithVatHtml,
  formatPeriod,
  placeTypeLabel,
  safeUrl
} from './format.js';
import { renderMapWidget } from './map-widget.js';
import { escapeHtml, renderNav, renderSourceBanner } from './nav.js';

renderNav('catalog');

const form = document.getElementById('filters');
const fieldset = form.querySelector('.catalog-controls');
const searchInput = document.getElementById('search');
const kindSelect = document.getElementById('kind');
const statusSelect = document.getElementById('status');
const methodSelect = document.getElementById('method');
const yearSelect = document.getElementById('year');
const buyerSelect = document.getElementById('buyer');
const repairCheckbox = document.getElementById('repair-only');

const resultsEl = document.getElementById('results');
const countEl = document.getElementById('result-count');
const paginationEl = document.getElementById('pagination');
const kpiEl = document.getElementById('kpi-strip');
const bannerEl = document.getElementById('catalog-mode');
const miniMapEl = document.getElementById('catalog-map');
const mapSummaryEl = document.getElementById('catalog-map-summary');

let currentPage = 1;
let searchTimer = null;

function readFiltersFromUrl() {
  const qs = new URLSearchParams(location.search);
  searchInput.value = qs.get('q') || '';
  kindSelect.value = qs.get('kind') || '';
  statusSelect.value = qs.get('status') || '';
  methodSelect.value = qs.get('method') || '';
  yearSelect.value = qs.get('year') || '';
  buyerSelect.value = qs.get('buyerEdrpou') || '';
  repairCheckbox.checked = qs.get('repairOnly') === 'true';
  currentPage = Math.max(1, Number(qs.get('page')) || 1);
}

function writeFiltersToUrl() {
  const qs = new URLSearchParams();
  if (searchInput.value.trim()) qs.set('q', searchInput.value.trim());
  if (kindSelect.value) qs.set('kind', kindSelect.value);
  if (statusSelect.value) qs.set('status', statusSelect.value);
  if (methodSelect.value) qs.set('method', methodSelect.value);
  if (yearSelect.value) qs.set('year', yearSelect.value);
  if (buyerSelect.value) qs.set('buyerEdrpou', buyerSelect.value);
  if (repairCheckbox.checked) qs.set('repairOnly', 'true');
  if (currentPage > 1) qs.set('page', String(currentPage));

  const next = qs.toString() ? `${location.pathname}?${qs.toString()}` : location.pathname;
  history.replaceState(null, '', next);
}

function currentParams() {
  return {
    search: searchInput.value.trim(),
    kind: kindSelect.value,
    status: statusSelect.value,
    method: methodSelect.value,
    year: yearSelect.value ? Number(yearSelect.value) : null,
    buyerEdrpou: buyerSelect.value,
    repairOnly: repairCheckbox.checked,
    pageNumber: currentPage,
    pageSize: CONFIG.pageSize
  };
}

async function populateDynamicSelects() {
  try {
    const [buyersAnalytics, yearsAnalytics] = await Promise.all([
      fetchAnalytics('buyer'),
      fetchAnalytics('year')
    ]);

    const selectedBuyer = new URLSearchParams(location.search).get('buyerEdrpou') || '';
    const selectedYear = new URLSearchParams(location.search).get('year') || '';

    for (const g of buyersAnalytics.groups || []) {
      const opt = document.createElement('option');
      opt.value = g.key;
      opt.textContent = `${g.label} (${g.procurementsCount})`;
      buyerSelect.append(opt);
    }
    buyerSelect.value = selectedBuyer;

    const years = (yearsAnalytics.groups || [])
      .filter((g) => /^\d{4}$/.test(g.key))
      .sort((a, b) => Number(b.key) - Number(a.key));
    for (const g of years) {
      const opt = document.createElement('option');
      opt.value = g.key;
      opt.textContent = `${g.key} (${g.procurementsCount})`;
      yearSelect.append(opt);
    }
    yearSelect.value = selectedYear;

    renderKpiStrip(buyersAnalytics.summary);
  } catch {
    // не блокуємо основний каталог
  }
}

function renderKpiStrip(summary) {
  if (!kpiEl || !summary) return;
  kpiEl.innerHTML = `
    <div class="panel kpi-card">
      <span>Усього закупівель</span>
      <strong>${summary.procurementsCount}</strong>
    </div>
    <div class="panel kpi-card">
      <span>Договір укладено</span>
      <strong>${summary.contractSignedCount}</strong>
    </div>
    <div class="panel kpi-card">
      <span>Триває процедура</span>
      <strong>${summary.activeCount ?? 0}</strong>
    </div>
    <div class="panel kpi-card">
      <span>Не відбулися / відмінені</span>
      <strong>${summary.notHeldCount + (summary.cancelledCount ?? 0)}</strong>
    </div>
  `;
}

function renderCards(items) {
  if (!items.length) {
    resultsEl.innerHTML = `
      <div class="panel empty-state">
        <h3>Закупівель не знайдено</h3>
        <p>Спробуйте змінити пошуковий запит або скинути фільтри.</p>
      </div>
    `;
    return;
  }

  resultsEl.innerHTML = items
    .map((p) => {
      const sourceHref = safeUrl(p.sourceUrl);
      const statusBadgeClass =
        p.status === 'contract_signed'
          ? 'badge'
          : p.status === 'active'
          ? 'badge badge-info'
          : 'badge badge-neutral';

      const coordNote = p.hasCoordinates
        ? `${placeTypeLabel(p.placeType)} · ${coordinatesStatusLabel(p.coordinatesStatus, true)}`
        : p.address
        ? `${placeTypeLabel(p.placeType)} · координат немає`
        : 'Місце не вказано';

      const gapsCount = p.missingData.length + p.needsCheck.length;

      return `
        <article class="panel procurement-card">
          <div class="card-top">
            <span class="eyebrow">ДК ${escapeHtml(p.cpvCode)} · ${escapeHtml(p.kind)} · ${escapeHtml(
              p.procurementType
            )}</span>
            <div class="card-badges">
              <span class="${statusBadgeClass}">${escapeHtml(p.statusLabel)}</span>
              <span class="badge badge-info">${escapeHtml(
                executionStatusLabel(p.executionStatus)
              )}</span>
            </div>
          </div>

          <h3><a href="./procurement.html?id=${encodeURIComponent(p.id)}">${escapeHtml(
            p.title
          )}</a></h3>
          <div class="record-id">${escapeHtml(p.id)}${p.year ? ` · ${p.year}` : ''}</div>

          <p class="buyer">
            <strong>Замовник:</strong> ${escapeHtml(p.buyer)} (ЄДРПОУ ${escapeHtml(p.buyerEdrpou)})<br>
            <strong>Виконавець:</strong> ${
              p.contractor
                ? `${escapeHtml(p.contractor)} (ЄДРПОУ ${escapeHtml(p.contractorEdrpou)})`
                : '<span class="muted">Не визначено</span>'
            }
          </p>

          <div class="card-facts">
            <div>
              <span class="fact-label">Очікувана вартість</span>
              ${formatMoneyWithVatHtml(p.expectedAmount, p.expectedVatIncluded, p.currency)}
            </div>
            <div>
              <span class="fact-label">Вартість договору</span>
              ${formatMoneyWithVatHtml(p.contractAmount, p.contractVatIncluded, p.currency)}
            </div>
          </div>

          <div class="card-meta-row">
            <span><strong>Адреса:</strong> ${escapeHtml(p.address || 'Не вказано')} (${escapeHtml(
              coordNote
            )})</span>
            <span><strong>Період поставки:</strong> ${escapeHtml(
              formatPeriod(p.periodStart, p.periodEnd)
            )}</span>
            ${
              gapsCount > 0
                ? `<span class="badge badge-warn">Уточнень і прогалин: ${gapsCount}</span>`
                : ''
            }
          </div>

          <div class="card-bottom">
            <a class="button button-secondary button-sm" href="./procurement.html?id=${encodeURIComponent(
              p.id
            )}">Відкрити картку →</a>
            ${
              sourceHref
                ? `<a href="${escapeHtml(sourceHref)}" target="_blank" rel="noopener noreferrer">Джерело (Prozorro) ↗</a>`
                : ''
            }
          </div>
        </article>
      `;
    })
    .join('');
}

function renderPagination(data) {
  if (!paginationEl) return;
  if (data.totalPages <= 1) {
    paginationEl.innerHTML = '';
    return;
  }

  paginationEl.innerHTML = `
    <span class="muted">Сторінка ${data.pageNumber} з ${data.totalPages} (усього ${data.totalCount})</span>
    <div style="display:flex;gap:8px;">
      <button type="button" class="button button-secondary button-sm" id="prev-page" ${
        data.pageNumber <= 1 ? 'disabled' : ''
      }>← Попередня</button>
      <button type="button" class="button button-secondary button-sm" id="next-page" ${
        data.pageNumber >= data.totalPages ? 'disabled' : ''
      }>Наступна →</button>
    </div>
  `;

  document.getElementById('prev-page')?.addEventListener('click', () => {
    currentPage = Math.max(1, currentPage - 1);
    refreshCatalog();
  });
  document.getElementById('next-page')?.addEventListener('click', () => {
    currentPage = Math.min(data.totalPages, currentPage + 1);
    refreshCatalog();
  });
}

async function refreshCatalog() {
  resultsEl.setAttribute('aria-busy', 'true');
  writeFiltersToUrl();

  try {
    const data = await fetchProcurements(currentParams());
    renderSourceBanner(bannerEl, getSourceInfo());
    countEl.textContent = `Знайдено закупівель: ${data.totalCount}`;
    renderCards(data.items);
    renderPagination(data);
  } catch (err) {
    countEl.textContent = 'Помилка завантаження';
    resultsEl.innerHTML = `
      <div class="panel empty-state">
        <h3>Не вдалося завантажити дані</h3>
        <p>${escapeHtml(err.message)}</p>
      </div>
    `;
  } finally {
    resultsEl.setAttribute('aria-busy', 'false');
  }
}

async function initMiniMap() {
  try {
    const { points, unmapped } = await fetchMapData();
    if (mapSummaryEl) {
      mapSummaryEl.textContent = `На мапі: ${points.length} закупівель (координати потребують перевірки). Без координат: ${unmapped.length}.`;
    }
    renderMapWidget(miniMapEl, points, { scrollWheelZoom: false });
  } catch {
    if (mapSummaryEl) mapSummaryEl.textContent = 'Не вдалося завантажити точки мапи.';
  }
}

document.getElementById('export-csv')?.addEventListener('click', () => downloadExport('csv'));
document.getElementById('export-json')?.addEventListener('click', () => downloadExport('json'));

searchInput.addEventListener('input', () => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    currentPage = 1;
    refreshCatalog();
  }, 220);
});

[kindSelect, statusSelect, methodSelect, yearSelect, buyerSelect, repairCheckbox].forEach(
  (el) => {
    el.addEventListener('change', () => {
      currentPage = 1;
      refreshCatalog();
    });
  }
);

form.addEventListener('reset', () => {
  setTimeout(() => {
    currentPage = 1;
    refreshCatalog();
  }, 0);
});

form.addEventListener('submit', (e) => {
  e.preventDefault();
  currentPage = 1;
  refreshCatalog();
});

async function init() {
  await populateDynamicSelects();
  readFiltersFromUrl();
  fieldset.disabled = false;
  await Promise.all([refreshCatalog(), initMiniMap()]);
}

init();