import { CONFIG } from './config.js';
import { escapeHtml } from './nav.js';
import { coordStatusText, formatArea, formatMoney } from './format.js';

export function groupPointsByCoords(points) {
  const map = new Map();
  for (const pt of points) {
    if (pt.latitude === null || pt.longitude === null) continue;
    const key = `${Number(pt.latitude).toFixed(5)},${Number(pt.longitude).toFixed(5)}`;
    if (!map.has(key)) map.set(key, { key, latitude: Number(pt.latitude), longitude: Number(pt.longitude), items: [] });
    map.get(key).items.push(pt);
  }
  return Array.from(map.values());
}

function pinColor(statusCode) {
  if (statusCode === 'free') return '#256149';
  if (statusCode === 'on_auction') return '#23536b';
  if (statusCode === 'rented') return '#896122';
  return '#66756e';
}

function popupHtml(item, shared = false) {
  return `
    <div class="map-popup-item">
      <div><strong><a href="./rental.html?id=${encodeURIComponent(item.ocid)}">${escapeHtml(item.title)}</a></strong></div>
      <div style="color:#56665f;margin:2px 0;">
        <code>${escapeHtml(item.ocid)}</code> · <strong>${escapeHtml(item.statusLabel)}</strong>
        ${item.areaSqm ? ` · ${escapeHtml(formatArea(item.areaSqm))}` : ''}
      </div>
      <div>${escapeHtml(item.address || 'Адресу не вказано')}</div>
      ${item.currentContractValue != null ? `<div>Договір: <strong>${escapeHtml(formatMoney(item.currentContractValue))}</strong></div>` : ''}
      <div style="font-size:11px;color:#56665f;margin-top:3px;">
        ${escapeHtml(coordStatusText(item.coordinatesStatus, true))}${shared ? ' · рознесено для зручності вибору' : ''}
      </div>
    </div>
  `;
}

export function renderMapWidget(container, points, options = {}) {
  if (!container) return null;
  container.innerHTML = '';
  const groups = groupPointsByCoords(points);
  if (groups.length === 0) {
    container.innerHTML = '<div class="empty-state"><p>Поки немає точок з координатами — триває збагачення з Prozorro.Продажі.</p></div>';
    return null;
  }

  if (typeof window.L !== 'undefined') return renderLeaflet(container, groups, options);
  return renderOfflineSvg(container, groups);
}

function renderLeaflet(container, groups, options) {
  const L = window.L;
  const center = groups.length === 1 ? [groups[0].latitude, groups[0].longitude] : CONFIG.poltavaCenter;
  const map = L.map(container, { scrollWheelZoom: Boolean(options.scrollWheelZoom) }).setView(center, options.zoom || CONFIG.defaultZoom);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; OpenStreetMap' }).addTo(map);

  const markerById = new Map();
  const bounds = [];

  for (const group of groups) {
    const basePos = [group.latitude, group.longitude];
    bounds.push(basePos);

    if (group.items.length === 1) {
      const item = group.items[0];
      const marker = L.circleMarker(basePos, { radius: 8, color: '#fff', weight: 2, fillColor: pinColor(item.statusCode), fillOpacity: 0.92 }).addTo(map);
      marker.bindPopup(popupHtml(item, false), { maxWidth: 320 });
      markerById.set(item.ocid, { marker, pos: basePos });
      continue;
    }

    const icon = L.divIcon({ className: 'civic-cluster-icon', html: `<div class="cluster-pin">${group.items.length}</div>`, iconSize: [30, 30], iconAnchor: [15, 15] });
    const centerMarker = L.marker(basePos, { icon }).addTo(map);
    centerMarker.bindPopup(group.items.slice(0, 12).map((i) => popupHtml(i, false)).join(''), { maxWidth: 320 });

    const count = group.items.length;
    group.items.forEach((item, idx) => {
      const angle = (2 * Math.PI * idx) / count - Math.PI / 2;
      const pos = [group.latitude + 0.00045 * Math.sin(angle), group.longitude + 0.0007 * Math.cos(angle)];
      bounds.push(pos);
      L.polyline([basePos, pos], { color: '#66756e', weight: 1.2, dashArray: '3, 4', opacity: 0.7 }).addTo(map);
      const marker = L.circleMarker(pos, { radius: 7, color: '#fff', weight: 2, fillColor: pinColor(item.statusCode), fillOpacity: 0.94 }).addTo(map);
      marker.bindPopup(popupHtml(item, true), { maxWidth: 320 });
      markerById.set(item.ocid, { marker, pos });
    });
  }

  if (bounds.length > 1) map.fitBounds(bounds, { padding: [32, 32], maxZoom: 15 });
  else if (options.openSinglePopup && groups[0].items.length === 1) markerById.get(groups[0].items[0].ocid)?.marker.openPopup();

  return {
    focusObject(ocid) {
      const e = markerById.get(ocid);
      if (!e) return;
      map.setView(e.pos, 16);
      e.marker.openPopup();
    },
    destroy() { map.remove(); }
  };
}

function renderOfflineSvg(container, groups) {
  const lats = groups.map((g) => g.latitude);
  const lngs = groups.map((g) => g.longitude);
  const minLat = Math.min(...lats, 49.55) - 0.01;
  const maxLat = Math.max(...lats, 49.62) + 0.01;
  const minLng = Math.min(...lngs, 34.48) - 0.015;
  const maxLng = Math.max(...lngs, 34.60) + 0.015;

  const project = (lat, lng) => [((lng - minLng) / (maxLng - minLng)) * 760 + 20, (1 - (lat - minLat) / (maxLat - minLat)) * 360 + 20];
  const wrap = document.createElement('div');
  wrap.className = 'offline-map';

  const pins = groups.map((g, idx) => {
    const [x, y] = project(g.latitude, g.longitude);
    const fill = pinColor(g.items[0].statusCode);
    return `<g class="svg-pin" data-idx="${idx}" tabindex="0" role="button" style="cursor:pointer;">
      <circle cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" r="${g.items.length > 1 ? 14 : 9}" fill="${fill}" stroke="#fff" stroke-width="2"></circle>
      ${g.items.length > 1 ? `<text x="${x.toFixed(1)}" y="${(y + 4).toFixed(1)}" text-anchor="middle" fill="#fff" font-size="11" font-weight="700">${g.items.length}</text>` : ''}
    </g>`;
  }).join('');

  wrap.innerHTML = `
    <svg viewBox="0 0 800 400" role="img" aria-label="Схематична мапа Полтави">
      <rect width="800" height="400" fill="#e8ede3"></rect>
      <path d="M 620 0 C 640 120, 690 220, 650 400" fill="none" stroke="#bfd3dc" stroke-width="18" opacity="0.85"/>
      <text x="20" y="28" fill="#56665f" font-size="12" font-weight="600">Полтава · об’єкти комунального майна</text>
      ${pins}
    </svg>
    <div class="offline-map-badge">Автономна схема Полтави</div>
    <div class="offline-map-popup" hidden></div>
  `;

  const popup = wrap.querySelector('.offline-map-popup');
  wrap.querySelectorAll('.svg-pin').forEach((pin) => {
    pin.addEventListener('click', () => {
      const g = groups[Number(pin.getAttribute('data-idx'))];
      popup.innerHTML = `<div style="display:flex;justify-content:space-between;margin-bottom:6px;"><strong>Точка</strong><button type="button" class="button button-secondary button-sm" data-close>✕</button></div>${g.items.slice(0, 10).map((i) => popupHtml(i)).join('')}`;
      popup.hidden = false;
      popup.querySelector('[data-close]').addEventListener('click', () => { popup.hidden = true; });
    });
  });

  container.append(wrap);
  return { focusObject() {}, destroy() { container.innerHTML = ''; } };
}