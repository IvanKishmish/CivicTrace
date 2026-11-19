import { fetchMapData, getSourceInfo } from './api.js';
import { formatMoneyWithVatHtml, placeTypeLabel } from './format.js';
import { renderMapWidget } from './map-widget.js';
import { escapeHtml, renderNav, renderSourceBanner } from './nav.js';

renderNav('map');

const bannerEl = document.getElementById('map-mode');
const mapContainer = document.getElementById('full-map');
const mappedListEl = document.getElementById('mapped-list');
const unmappedListEl = document.getElementById('unmapped-list');
const placeFilterEl = document.getElementById('map-place-filter');
const statusFilterEl = document.getElementById('map-status-filter');
const mapStatsEl = document.getElementById('map-stats');

let allPoints = [];
let unmappedItems = [];
let mapHandle = null;

function applyMapFilters() {
  const place = placeFilterEl.value;
  const status = statusFilterEl.value;

  const filtered = allPoints.filter((pt) => {
    if (place && pt.placeType !== place) return false;
    if (status && pt.status !== status) return false;
    return true;
  });

  mapStatsEl.textContent = `Показано на мапі: ${filtered.length} із ${allPoints.length} точок із координатами · Без координат: ${unmappedItems.length}`;

  if (mapHandle) mapHandle.destroy();
  mapHandle = renderMapWidget(mapContainer, filtered, { scrollWheelZoom: true });

  if (!filtered.length) {
    mappedListEl.innerHTML =
      '<p class="muted">За вибраними фільтрами немає точок із координатами.</p>';
  } else {
    mappedListEl.innerHTML = filtered
      .map((pt) => {
        const amount =
          pt.contractAmount != null
            ? formatMoneyWithVatHtml(pt.contractAmount, pt.contractVatIncluded, pt.currency)
            : formatMoneyWithVatHtml(pt.expectedAmount, pt.expectedVatIncluded, pt.currency);

        return `
          <div class="panel" style="padding:14px;margin-bottom:10px;">
            <div style="display:flex;justify-content:space-between;gap:8px;align-items:start;">
              <strong style="font-size:13px;">${escapeHtml(pt.title)}</strong>
              <span class="badge badge-warn">${escapeHtml(placeTypeLabel(pt.placeType))}</span>
            </div>
            <div class="small-note" style="margin:6px 0;">
              ${escapeHtml(pt.address || 'Адресу не вказано')} · <code>${escapeHtml(pt.id)}</code>
            </div>
            <div style="margin-bottom:8px;">${amount}</div>
            <div style="display:flex;gap:8px;flex-wrap:wrap;">
              <button type="button" class="button button-secondary button-sm" data-focus-id="${escapeHtml(
                pt.id
              )}">Показати на мапі</button>
              <a class="button button-sm" href="./procurement.html?id=${encodeURIComponent(
                pt.id
              )}">Картка →</a>
            </div>
          </div>
        `;
      })
      .join('');

    mappedListEl.querySelectorAll('[data-focus-id]').forEach((btn) => {
      btn.addEventListener('click', () => {
        mapHandle?.focusProcurement(btn.getAttribute('data-focus-id'));
      });
    });
  }
}

function renderUnmapped() {
  if (!unmappedItems.length) {
    unmappedListEl.innerHTML = '<p class="muted">Усі закупівлі мають координати.</p>';
    return;
  }

  unmappedListEl.innerHTML = unmappedItems
    .map(
      (p) => `
      <div class="panel" style="padding:14px;margin-bottom:10px;">
        <div style="display:flex;justify-content:space-between;gap:8px;align-items:start;">
          <a href="./procurement.html?id=${encodeURIComponent(p.id)}"><strong>${escapeHtml(
            p.title
          )}</strong></a>
          <span class="badge badge-neutral">${escapeHtml(p.statusLabel)}</span>
        </div>
        <div class="small-note" style="margin-top:4px;">
          <code>${escapeHtml(p.id)}</code> · ${
            p.address
              ? `Адреса «${escapeHtml(p.address)}» ще не геокодована`
              : 'Адресу поставки/робіт у джерелі не вказано'
          }
        </div>
      </div>
    `
    )
    .join('');
}

async function init() {
  const data = await fetchMapData();
  renderSourceBanner(bannerEl, getSourceInfo());
  allPoints = data.points;
  unmappedItems = data.unmapped;
  applyMapFilters();
  renderUnmapped();
}

placeFilterEl.addEventListener('change', applyMapFilters);
statusFilterEl.addEventListener('change', applyMapFilters);

init();