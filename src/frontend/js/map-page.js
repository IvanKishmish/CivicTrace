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

let allPoints = [];
let mapMeta = null;
let handle = null;

function apply() {
  const st = statusFilter.value;
  const minA = Number(minAreaFilter.value) || 0;
  const filtered = allPoints.filter((p) => (!st || p.statusCode === st) && (!minA || (p.areaSqm ?? 0) >= minA));

  statsEl.innerHTML = `
    Усього в P3-01: <strong>${mapMeta.totalObjects}</strong> ·
    На мапі зараз: <strong>${filtered.length}</strong> (із ${mapMeta.mappedCount} геокодованих) ·
    Негеокодованих: <strong>${mapMeta.ungeocodedPercent}%</strong> (з адресою без GPS: ${mapMeta.unmappedWithAddressCount}, без адреси/збагачення: ${mapMeta.unmappedWithoutAddressCount})
  `;

  if (handle) handle.destroy();
  handle = renderMapWidget(mapContainer, filtered, { scrollWheelZoom: true });

  listEl.innerHTML = filtered.slice(0, 40).map((p) => `
    <div class="panel" style="padding:12px;margin-bottom:8px;">
      <strong><a href="./rental.html?id=${encodeURIComponent(p.ocid)}">${escapeHtml(p.title)}</a></strong>
      <div class="small-note">${escapeHtml(p.statusLabel)} · ${escapeHtml(formatArea(p.areaSqm))} · ${escapeHtml(p.address || '')}</div>
      <button type="button" class="button button-secondary button-sm" data-focus="${escapeHtml(p.ocid)}" style="margin-top:6px;">Показати на мапі</button>
    </div>
  `).join('') || '<p class="muted">Немає точок за вибраним фільтром.</p>';

  listEl.querySelectorAll('[data-focus]').forEach((b) => {
    b.addEventListener('click', () => handle?.focusObject(b.getAttribute('data-focus')));
  });
}

async function init() {
  mapMeta = await fetchRentalMap();
  allPoints = mapMeta.points;
  renderLastUpdatedBanner(bannerEl, null, `Негеокодованих об’єктів: ${mapMeta.ungeocodedPercent}%`);
  apply();
}

statusFilter.addEventListener('change', apply);
minAreaFilter.addEventListener('input', apply);
init();