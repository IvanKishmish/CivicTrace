import { CONFIG } from './config.js';
import { escapeHtml } from './nav.js';
import {
  coordinatesStatusLabel,
  formatMoneyWithVatHtml,
  placeTypeLabel
} from './format.js';

export function groupPointsByCoords(points) {
  const map = new Map();
  for (const pt of points) {
    if (pt.latitude === null || pt.longitude === null) continue;
    const key = `${Number(pt.latitude).toFixed(5)},${Number(pt.longitude).toFixed(5)}`;
    if (!map.has(key)) {
      map.set(key, {
        key,
        latitude: Number(pt.latitude),
        longitude: Number(pt.longitude),
        items: []
      });
    }
    map.get(key).items.push(pt);
  }
  return Array.from(map.values());
}

export function buildSinglePopupHtml(item, sharedAddressNote = false) {
  const amountHtml =
    item.contractAmount != null
      ? formatMoneyWithVatHtml(item.contractAmount, item.contractVatIncluded, item.currency)
      : item.expectedAmount != null
      ? `Очікувана: ${formatMoneyWithVatHtml(
          item.expectedAmount,
          item.expectedVatIncluded,
          item.currency
        )}`
      : 'Суму не вказано';

  return `
    <div class="map-popup-item">
      <div style="margin-bottom:4px;"><strong><a href="./procurement.html?id=${encodeURIComponent(item.id)}">${escapeHtml(
        item.title
      )}</a></strong></div>
      <div style="color:#5a6a63; margin-bottom:4px;">
        <code>${escapeHtml(item.id)}</code> · ${escapeHtml(placeTypeLabel(item.placeType))}
        ${item.statusLabel ? ` · ${escapeHtml(item.statusLabel)}` : ''}
      </div>
      <div style="margin-bottom:4px;">${escapeHtml(item.address || 'Адресу не вказано')}</div>
      <div>${amountHtml}</div>
      <div style="color:#896122;font-size:11px;margin-top:4px;">
        ${escapeHtml(coordinatesStatusLabel(item.coordinatesStatus, true))}
        ${sharedAddressNote ? ' · точка візуально рознесена для зручності вибору' : ''}
      </div>
    </div>
  `;
}

export function buildGroupPopupHtml(group) {
  const first = group.items[0];
  const header = `
    <div style="margin-bottom:8px;">
      <strong>${escapeHtml(first.address || 'Адресу не вказано')}</strong><br>
      <span style="color:#5a6a63;">Закупівель за цією адресою: ${group.items.length} · ${escapeHtml(
        coordinatesStatusLabel(first.coordinatesStatus, true)
      )}</span>
    </div>
  `;
  const itemsHtml = group.items.map((item) => buildSinglePopupHtml(item, false)).join('');
  return `${header}${itemsHtml}`;
}

export function renderMapWidget(container, points, options = {}) {
  if (!container) return null;
  container.innerHTML = '';

  const groups = groupPointsByCoords(points);
  if (groups.length === 0) {
    container.innerHTML = `
      <div class="empty-state" style="padding:28px;">
        <p>Немає записів із доданими координатами для відображення на мапі.</p>
      </div>
    `;
    return null;
  }

  if (typeof window.L !== 'undefined') {
    return renderLeafletMap(container, groups, options);
  }
  return renderOfflineSvgMap(container, groups);
}

function renderLeafletMap(container, groups, options) {
  const L = window.L;
  const center =
    groups.length === 1
      ? [groups[0].latitude, groups[0].longitude]
      : CONFIG.poltavaCenter;

  const map = L.map(container, { scrollWheelZoom: Boolean(options.scrollWheelZoom) }).setView(
    center,
    options.zoom || CONFIG.defaultZoom
  );

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; OpenStreetMap'
  }).addTo(map);

  const markerById = new Map();
  const bounds = [];

  for (const group of groups) {
    const basePos = [group.latitude, group.longitude];
    bounds.push(basePos);

    if (group.items.length === 1) {
      const item = group.items[0];
      const color = item.placeType === 'work' ? '#896122' : '#256149';
      const marker = L.circleMarker(basePos, {
        radius: 9,
        color: '#ffffff',
        weight: 2,
        fillColor: color,
        fillOpacity: 0.92
      }).addTo(map);

      marker.bindPopup(buildSinglePopupHtml(item, false), { maxWidth: 320 });
      markerById.set(item.id, { marker, pos: basePos });
      continue;
    }

    // Якщо кілька закупівель на одній координаті — розносимо їх по колу (spiderfy),
    // щоб усі маркери були видимими й клікабельними окремо, а центр показує спільний список.
    const centerIcon = L.divIcon({
      className: 'civic-cluster-icon',
      html: `<div class="cluster-pin">${group.items.length}</div>`,
      iconSize: [30, 30],
      iconAnchor: [15, 15]
    });
    const centerMarker = L.marker(basePos, { icon: centerIcon }).addTo(map);
    centerMarker.bindPopup(buildGroupPopupHtml(group), { maxWidth: 320 });

    const count = group.items.length;
    const radiusLat = 0.00055;
    const radiusLng = 0.00085;

    group.items.forEach((item, index) => {
      const angle = (2 * Math.PI * index) / count - Math.PI / 2;
      const pos = [
        group.latitude + radiusLat * Math.sin(angle),
        group.longitude + radiusLng * Math.cos(angle)
      ];
      bounds.push(pos);

      L.polyline([basePos, pos], {
        color: '#66756e',
        weight: 1.5,
        dashArray: '3, 4',
        opacity: 0.8
      }).addTo(map);

      const color = item.placeType === 'work' ? '#896122' : '#256149';
      const marker = L.circleMarker(pos, {
        radius: 8,
        color: '#ffffff',
        weight: 2,
        fillColor: color,
        fillOpacity: 0.94
      }).addTo(map);

      marker.bindPopup(buildSinglePopupHtml(item, true), { maxWidth: 320 });
      markerById.set(item.id, { marker, pos });
    });
  }

  if (bounds.length > 1) {
    map.fitBounds(bounds, { padding: [36, 36], maxZoom: 16 });
  } else if (options.openSinglePopup && groups.length === 1 && groups[0].items.length === 1) {
    const firstId = groups[0].items[0].id;
    markerById.get(firstId)?.marker.openPopup();
  }

  return {
    focusProcurement(id) {
      const entry = markerById.get(id);
      if (!entry) return;
      map.setView(entry.pos, 16);
      entry.marker.openPopup();
    },
    destroy() {
      map.remove();
    }
  };
}

function renderOfflineSvgMap(container, groups) {
  const lats = groups.map((g) => g.latitude);
  const lngs = groups.map((g) => g.longitude);
  const minLat = Math.min(...lats, 49.565) - 0.012;
  const maxLat = Math.max(...lats, 49.61) + 0.012;
  const minLng = Math.min(...lngs, 34.51) - 0.015;
  const maxLng = Math.max(...lngs, 34.59) + 0.015;

  const project = (lat, lng) => {
    const x = ((lng - minLng) / (maxLng - minLng)) * 760 + 20;
    const y = (1 - (lat - minLat) / (maxLat - minLat)) * 360 + 20;
    return [x, y];
  };

  const wrap = document.createElement('div');
  wrap.className = 'offline-map';

  const pinsSvg = groups
    .map((group, idx) => {
      const [x, y] = project(group.latitude, group.longitude);
      const count = group.items.length;
      const fill = group.items.some((i) => i.placeType === 'work') ? '#896122' : '#256149';
      return `
        <g class="svg-pin" data-idx="${idx}" tabindex="0" role="button"
           aria-label="${escapeHtml(group.items[0].address || 'Точка на мапі')}"
           style="cursor:pointer;">
          <circle cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" r="${count > 1 ? 16 : 11}"
                  fill="${fill}" stroke="#ffffff" stroke-width="2.5"></circle>
          ${
            count > 1
              ? `<text x="${x.toFixed(1)}" y="${(y + 4).toFixed(1)}" text-anchor="middle"
                       fill="#ffffff" font-size="12" font-weight="700">${count}</text>`
              : ''
          }
        </g>
      `;
    })
    .join('');

  wrap.innerHTML = `
    <svg viewBox="0 0 800 400" role="img" aria-label="Схематична мапа Полтави (автономний режим)">
      <rect width="800" height="400" fill="#e8ede3"></rect>
      <g stroke="#d6e0d0" stroke-width="1">
        <line x1="0" y1="100" x2="800" y2="100"/><line x1="0" y1="200" x2="800" y2="200"/>
        <line x1="0" y1="300" x2="800" y2="300"/>
        <line x1="200" y1="0" x2="200" y2="400"/><line x1="400" y1="0" x2="400" y2="400"/>
        <line x1="600" y1="0" x2="600" y2="400"/>
      </g>
      <path d="M 620 0 C 640 120, 690 220, 650 400" fill="none" stroke="#bfd3dc" stroke-width="18" opacity="0.85"/>
      <text x="666" y="340" fill="#4f6d7a" font-size="11">р. Ворскла</text>
      <text x="24" y="32" fill="#5a6a63" font-size="12" font-weight="600">Полтава · координатна схема</text>
      ${pinsSvg}
    </svg>
    <div class="offline-map-badge">Автономна схема Полтави (Leaflet CDN недоступний)</div>
    <div class="offline-map-popup" hidden></div>
  `;

  const popupEl = wrap.querySelector('.offline-map-popup');
  const openGroup = (idx) => {
    const group = groups[idx];
    if (!group) return;
    popupEl.innerHTML = `
      <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px;">
        <strong style="font-size:11px;color:#5a6a63;">Деталі точки</strong>
        <button type="button" class="button button-secondary button-sm" data-close-popup>✕</button>
      </div>
      ${buildGroupPopupHtml(group)}
    `;
    popupEl.hidden = false;
    popupEl.querySelector('[data-close-popup]')?.addEventListener('click', () => {
      popupEl.hidden = true;
    });
  };

  wrap.querySelectorAll('.svg-pin').forEach((pin) => {
    const idx = Number(pin.getAttribute('data-idx'));
    pin.addEventListener('click', () => openGroup(idx));
    pin.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        openGroup(idx);
      }
    });
  });

  if (groups.length === 1) openGroup(0);

  container.append(wrap);

  return {
    focusProcurement(id) {
      const idx = groups.findIndex((g) => g.items.some((i) => i.id === id));
      if (idx >= 0) openGroup(idx);
    },
    destroy() {
      container.innerHTML = '';
    }
  };
}