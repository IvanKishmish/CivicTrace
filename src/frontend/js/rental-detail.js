import { fetchRentalByOcid } from './api.js';
import { coordStatusText, formatArea, formatDate, formatMoney, listTypeLabel, safeUrl, statusBadgeClass } from './format.js';
import { renderMapWidget } from './map-widget.js';
import { escapeHtml, renderLastUpdatedBanner, renderNav } from './nav.js';

renderNav('catalog');

const detailEl = document.getElementById('detail');
const bannerEl = document.getElementById('detail-mode');
const ocid = new URLSearchParams(location.search).get('id');

async function load() {
  if (!ocid) {
    detailEl.innerHTML = '<section class="panel empty-state"><h2>Не вказано ID об’єкта</h2><p><a href="./index.html">До каталогу</a></p></section>';
    return;
  }

  const r = await fetchRentalByOcid(ocid);
  detailEl.setAttribute('aria-busy', 'false');

  if (!r) {
    detailEl.innerHTML = `<section class="panel empty-state"><h2>Об’єкт ${escapeHtml(ocid)} не знайдено</h2><p><a href="./index.html">До каталогу</a></p></section>`;
    return;
  }

  document.title = `${r.title} — CivicTrace`;
  renderLastUpdatedBanner(bannerEl, r.lastSyncedAt);

  const canonical = safeUrl(r.canonicalUrl);
  const risksHtml = r.riskFlags.length === 0
    ? '<p class="small-note">Індикаторів ризику не зафіксовано (або об’єкт ще не проходив аукціонів).</p>'
    : r.riskFlags.map((f) => `
        <div class="notice notice-risk">
          <strong>${escapeHtml(f.title)}</strong> <span class="badge badge-risk">Вага: ${f.weight}</span><br>
          <span>${escapeHtml(f.reason)}</span><br>
          <span class="small-note"><em>Легітимне пояснення:</em> ${escapeHtml(f.legitimateExplanation)}</span>
        </div>
      `).join('');

  const auctionsHtml = r.auctions.length === 0
    ? '<p class="muted">Пов’язаних аукціонів у Prozorro.Продажі поки не знайдено.</p>'
    : `<div class="table-wrap"><table class="data-table">
        <thead><tr><th>Аукціон</th><th>Статус</th><th>Спроба</th><th class="num">Старт</th><th class="num">Фінал</th><th class="num">грн/м²</th><th class="num">Учасн.</th><th class="num">Ризик</th></tr></thead>
        <tbody>
          ${r.auctions.map((a) => `
            <tr>
              <td><a href="${escapeHtml(safeUrl(a.sourceUrl) || '#')}" target="_blank" rel="noopener noreferrer"><code>${escapeHtml(a.auctionId)}</code> ↗</a><br><span class="small-note">${escapeHtml(formatDate(a.auctionDate || a.datePublished))}</span></td>
              <td>${escapeHtml(a.statusLabel)}</td>
              <td>${a.tenderAttempts ?? '—'}</td>
              <td class="num">${escapeHtml(formatMoney(a.startPrice))}</td>
              <td class="num">${escapeHtml(formatMoney(a.finalPrice))}</td>
              <td class="num">${a.pricePerSqmFinal != null ? escapeHtml(formatMoney(a.pricePerSqmFinal)) : '—'}</td>
              <td class="num">${a.bidsCount}</td>
              <td class="num">${a.riskScore > 0 ? `<span class="badge badge-risk">${a.riskScore}</span>` : '0'}</td>
            </tr>
          `).join('')}
        </tbody>
      </table></div>`;

  detailEl.innerHTML = `
    <div class="detail-heading">
      <div class="card-badges">
        <span class="${statusBadgeClass(r.statusCode)}">${escapeHtml(r.statusLabel)}</span>
        <span class="badge badge-neutral">${escapeHtml(listTypeLabel(r.listType))}</span>
        <span class="badge badge-neutral">${escapeHtml(r.objectType)}</span>
        ${r.riskScore > 0 ? `<span class="badge badge-risk">Бал ознак ризику: ${r.riskScore}</span>` : ''}
      </div>
      <h1 class="page-title">${escapeHtml(r.title)}</h1>
      <div class="small-note">
        ID об’єкта в Prozorro.Продажі: <strong>${escapeHtml(r.ocid)}</strong>
        ${r.apiId ? ` · API ID: <code>${escapeHtml(r.apiId)}</code>` : ''}
        · Початкова назва у переліку міста: <em>«${escapeHtml(r.rawTitle)}»</em>
      </div>
    </div>

    <div class="detail-grid">
      <div>
        <section class="panel">
          <h2>Відомості про об’єкт оренди</h2>
          <dl class="facts">
            <div><dt>Адреса</dt><dd>${escapeHtml(r.address || (r.enrichmentStatus === 'NotFoundInApi' ? 'API не повернуло адресу' : 'Адресу не знайдено'))}${r.district ? `<br><span class="small-note">${escapeHtml(r.district)}</span>` : ''}</dd></div>
            <div><dt>Площа</dt><dd><strong>${escapeHtml(formatArea(r.areaSqm))}</strong></dd></div>
            <div><dt>Балансоутримувач</dt><dd>${escapeHtml(r.propertyOwnerName || 'Не вказано')}${r.propertyOwnerEdrpou ? `<br><span class="small-note">ЄДРПОУ ${escapeHtml(r.propertyOwnerEdrpou)}</span>` : ''}</dd></div>
            <div><dt>Орендодавець</dt><dd>${escapeHtml(r.sellingEntityName || 'Не вказано')}${r.sellingEntityEdrpou ? `<br><span class="small-note">ЄДРПОУ ${escapeHtml(r.sellingEntityEdrpou)}</span>` : ''}</dd></div>
            <div><dt>Ринкова вартість</dt><dd>${escapeHtml(formatMoney(r.marketValue))}</dd></div>
            <div><dt>Строк оренди / Суборенда</dt><dd>${escapeHtml(r.leaseDuration || 'Не вказано')} · Суборенда: ${r.subleaseAllowed === true ? 'Так' : r.subleaseAllowed === false ? 'Ні' : 'Не вказано'}</dd></div>
            <div><dt>Чинний договір та орендар</dt><dd>${r.currentContractNumber ? `№ ${escapeHtml(r.currentContractNumber)} (${escapeHtml(r.currentContractMethod || '')}) · ${escapeHtml(formatMoney(r.currentContractValue))}<br><span class="small-note">${escapeHtml(r.currentTenantLabel || '')} · до ${escapeHtml(formatDate(r.currentContractEnd))}</span>` : '<span class="muted">Чинного договору не зазначено</span>'}</dd></div>
            <div><dt>Першоджерело</dt><dd>${canonical ? `<a href="${escapeHtml(canonical)}" target="_blank" rel="noopener noreferrer">Сторінка в Prozorro.Продажі ↗</a>` : '—'}</dd></div>
          </dl>
        </section>

        <section class="panel">
          <h2>Історія аукціонів об’єкта</h2>
          ${auctionsHtml}
        </section>

        <section class="panel">
          <h2>Ознаки ризику (підстава для перевірки)</h2>
          <p class="small-note">Сигнал ризику є підставою для перевірки й сам по собі не доводить порушення.</p>
          ${risksHtml}
        </section>
      </div>

      <aside>
        <section class="panel">
          <h2>Розташування на мапі</h2>
          <div class="notice ${r.hasCoordinates ? 'notice-ok' : ''}">
            ${escapeHtml(coordStatusText(r.coordinatesStatus, r.hasCoordinates))}
            ${r.hasCoordinates ? ` (${r.latitude.toFixed(5)}, ${r.longitude.toFixed(5)})` : ''}
          </div>
          <div id="detail-map" class="map-box"></div>
        </section>

        <section class="panel">
          <h2>Якість вихідних даних міського переліку</h2>
          ${r.dataDefectsNotes ? `<div class="notice">${escapeHtml(r.dataDefectsNotes)}</div>` : '<p class="small-note">Дефектів URL/назви у цьому рядку не виявлено.</p>'}
          <p class="small-note">Оригінальне посилання з Excel: <code>${escapeHtml(r.originalUrl)}</code></p>
        </section>
      </aside>
    </div>
  `;

  renderMapWidget(document.getElementById('detail-map'), r.hasCoordinates ? [r] : [], { zoom: 16, openSinglePopup: true });
}

async function refresh() {
  detailEl.setAttribute('aria-busy', 'true');
  try { await load(); }
  catch (error) {
    detailEl.innerHTML = `<section class="panel empty-state"><h2>Не вдалося завантажити об’єкт</h2><p>${escapeHtml(error.message)}</p><button type="button" class="button" id="retry-detail">Спробувати ще раз</button></section>`;
    document.getElementById('retry-detail').addEventListener('click', refresh);
  } finally { detailEl.setAttribute('aria-busy', 'false'); }
}
refresh();
