import { fetchRentalMap } from './api.js';
import { formatArea } from './format.js';
import { renderMapWidget } from './map-widget.js';
import { escapeHtml, renderLastUpdatedBanner, renderNav } from './nav.js';

renderNav('map');

const bannerEl = document.getElementById('map-mode');
const mapContainer = document.getElementById('full-map');
const statsEl = document.getElementById('map-stats');
const listEl = document.getElementById('mapped-list');
const statusFilter = document.getElementById('map-status-filter');
const minAreaFilter = document.getElementById('map-min-area');
const refreshBtn = document.getElementById('refresh-map');
const riskOnlyFilter = document.getElementById('map-risk-only');

let allPoints = [];
let mapMeta = null;
let handle = null;
let retryTimer = null;
let retryCount = 0;
const retryLimit = 8;

function apply() {
  if (!mapMeta) return;
  const st = statusFilter.value;
  const minA = Number(minAreaFilter.value) || 0;
  const riskOnly = riskOnlyFilter.checked;
  const filtered = allPoints.filter((p) => (!st || p.statusCode === st) && (!minA || (p.areaSqm ?? 0) >= minA) && (!riskOnly || p.riskScore > 0));

  statsEl.innerHTML = `
    Усього в P3-01: <strong>${mapMeta.totalObjects}</strong> ·
    На мапі зараз: <strong>${filtered.length}</strong> (із ${mapMeta.mappedCount} геокодованих; із сигналами: ${allPoints.filter((p) => p.riskScore > 0).length}) ·
    Негеокодованих: <strong>${mapMeta.ungeocodedPercent}%</strong> (з адресою без GPS: ${mapMeta.unmappedWithAddressCount}, без адреси/збагачення: ${mapMeta.unmappedWithoutAddressCount})
  `;

  if (handle) handle.destroy();
  handle = renderMapWidget(mapContainer, filtered, { scrollWheelZoom: true });

  listEl.innerHTML = filtered.slice(0, 40).map((p) => `
    <div class="panel" style="padding:12px;margin-bottom:8px;">
      <strong><a href="./rental.html?id=${encodeURIComponent(p.ocid)}">${escapeHtml(p.title)}</a></strong>
      <div class="small-note">${escapeHtml(p.statusLabel)} · ${escapeHtml(formatArea(p.areaSqm))} · ${escapeHtml(p.address || '')}</div>
      ${p.riskScore > 0 ? `<div style="margin-top:6px;"><span class="badge badge-risk">${p.riskFlags.length} сигналів · бал ${p.riskScore}</span></div>` : ''}
      <button type="button" class="button button-secondary button-sm" data-focus="${escapeHtml(p.ocid)}" style="margin-top:6px;">Показати на мапі</button>
    </div>
  `).join('') || '<p class="muted">Немає точок за вибраним фільтром.</p>';

  listEl.querySelectorAll('[data-focus]').forEach((b) => {
    b.addEventListener('click', () => handle?.focusObject(b.getAttribute('data-focus')));
  });
}

async function init({ automatic = false } = {}) {
  if (!automatic) {
    retryCount = 0;
    clearTimeout(retryTimer);
  }
  refreshBtn.disabled = true;
  if (!automatic) statsEl.textContent = 'Оновлюємо стан карти…';

  try {
    mapMeta = await fetchRentalMap();
    allPoints = mapMeta.points;
    renderLastUpdatedBanner(bannerEl, null, `Негеокодованих об’єктів: ${mapMeta.ungeocodedPercent}%`);
    apply();

    if (mapMeta.totalObjects > 0 && mapMeta.mappedCount === 0 && retryCount < retryLimit) {
      retryCount += 1;
      retryTimer = setTimeout(() => init({ automatic: true }), 15000);
    }
  } catch (err) {
    mapMeta = null;
    allPoints = [];
    statsEl.textContent = `Не вдалося завантажити карту: ${err.message}. Перевірте API або натисніть «Оновити дані карти».`;
    listEl.innerHTML = '<p class="muted">Перелік точок зараз недоступний.</p>';
    handle?.destroy();
    handle = null;
    if (retryCount < retryLimit) {
      retryCount += 1;
      retryTimer = setTimeout(() => init({ automatic: true }), 15000);
    }
  } finally {
    refreshBtn.disabled = false;
  }
}

refreshBtn.addEventListener('click', () => init());
statusFilter.addEventListener('change', apply);
minAreaFilter.addEventListener('input', apply);
riskOnlyFilter.addEventListener('change', apply);
init();