import {
  fetchProcurementById,
  getAuthState,
  getSourceInfo,
  tryRefreshSession,
  verifyProcurement
} from './api.js';
import {
  bidOutcomeLabel,
  coordinatesStatusLabel,
  executionStatusLabel,
  formatDate,
  formatMoney,
  formatMoneyWithVatHtml,
  formatPeriod,
  placeTypeLabel,
  safeUrl,
  stageLabel,
  vatLabel
} from './format.js';
import { renderMapWidget } from './map-widget.js';
import { escapeHtml, renderNav, renderSourceBanner } from './nav.js';

export function renderProcurementDetailHtml(p, options = {}) {
  const sourceHref = safeUrl(p.sourceUrl);
  const statusClass = p.status === 'contract_signed' ? 'badge' : p.status === 'active' ? 'badge badge-info' : 'badge badge-neutral';

  const fundingHtml =
    p.funding.length === 0
      ? '<p class="muted">Відомостей про джерела фінансування не знайдено.</p>'
      : `
        <div class="table-wrap">
          <table class="data-table">
            <thead>
              <tr>
                <th>Джерело</th>
                <th>Код видатків</th>
                <th>Територія</th>
                <th class="num">Сума</th>
              </tr>
            </thead>
            <tbody>
              ${p.funding
                .map(
                  (f) => `
                <tr>
                  <td>${escapeHtml(f.source)}</td>
                  <td>${escapeHtml(f.expenditureCode || 'Не вказано')}</td>
                  <td>${escapeHtml(f.territory || 'Не вказано')}</td>
                  <td class="num">${escapeHtml(formatMoney(f.amount, p.currency))}</td>
                </tr>
              `
                )
                .join('')}
            </tbody>
          </table>
        </div>
      `;

  const basisHtml = p.nonCompetitiveBasis
    ? `
      <section class="panel">
        <h2>Підстава неконкурентної закупівлі</h2>
        <dl class="facts">
          <div>
            <dt>Нормативна підстава</dt>
            <dd>${escapeHtml(p.nonCompetitiveBasis.legalBasis)}</dd>
          </div>
          <div>
            <dt>Обґрунтування з джерела</dt>
            <dd>${escapeHtml(p.nonCompetitiveBasis.justification)}</dd>
          </div>
        </dl>
        ${
          safeUrl(p.nonCompetitiveBasis.sourceUrl)
            ? `<p class="small-note" style="margin-top:12px;"><a href="${escapeHtml(
                safeUrl(p.nonCompetitiveBasis.sourceUrl)
              )}" target="_blank" rel="noopener noreferrer">Підтвердження у Prozorro ↗</a></p>`
            : ''
        }
      </section>
    `
    : '';

  let bidsHtml = '';
  if (p.bids.length === 0) {
    bidsHtml =
      '<p class="muted">Відомостей про подані пропозиції на сторінці не показано.</p>';
  } else if (p.bidsListComplete) {
    bidsHtml = `
      <p class="small-note">Показано повний реєстр розкритих пропозицій (${p.bids.length}).</p>
      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>Учасник</th>
              <th>ЄДРПОУ</th>
              <th class="num">Сума пропозиції</th>
              <th>Результат</th>
            </tr>
          </thead>
          <tbody>
            ${p.bids
              .map(
                (b) => `
              <tr>
                <td>${escapeHtml(b.bidder)}</td>
                <td>${escapeHtml(b.bidderEdrpou)}</td>
                <td class="num">${escapeHtml(formatMoney(b.amount, p.currency))} <span class="vat-tag">${escapeHtml(
                  vatLabel(b.vatIncluded)
                )}</span></td>
                <td>${
                  b.outcome === 'winner'
                    ? `<span class="badge">${escapeHtml(bidOutcomeLabel(b.outcome))}</span>`
                    : b.outcome === 'rejected'
                    ? `<span class="badge badge-warn">${escapeHtml(
                        bidOutcomeLabel(b.outcome)
                      )}</span>`
                    : '<span class="muted">Не зазначено</span>'
                }</td>
              </tr>
            `
              )
              .join('')}
          </tbody>
        </table>
      </div>
    `;
  } else {
    bidsHtml = `
      <p class="small-note">Повний реєстр пропозицій на сторінці не оприлюднено (кількість учасників не вказується); відображено наявну інформацію з протоколу:</p>
      <ul class="clean-list">
        ${p.bids
          .map(
            (b) =>
              `<li><strong>${escapeHtml(b.bidder)}</strong> (ЄДРПОУ ${escapeHtml(
                b.bidderEdrpou
              )}) — ${escapeHtml(formatMoney(b.amount, p.currency))} (${escapeHtml(
                vatLabel(b.vatIncluded)
              )})</li>`
          )
          .join('')}
      </ul>
    `;
  }

  const eventsHtml =
    p.events.length === 0
      ? '<p class="muted">Події відсутні.</p>'
      : `
        <ol class="timeline">
          ${p.events
            .map((e) => {
              const evUrl = safeUrl(e.sourceUrl);
              return `
                <li>
                  <div class="timeline-date">
                    ${escapeHtml(formatDate(e.date))}
                    <span class="badge badge-neutral" style="margin-left:6px;">${escapeHtml(
                      stageLabel(e.stage)
                    )}</span>
                  </div>
                  <h3>${escapeHtml(e.title)}</h3>
                  ${
                    e.dateNote
                      ? `<p class="small-note" style="margin:4px 0;">${escapeHtml(e.dateNote)}</p>`
                      : ''
                  }
                  ${
                    evUrl
                      ? `<a class="timeline-source" href="${escapeHtml(
                          evUrl
                        )}" target="_blank" rel="noopener noreferrer">Джерело ↗</a>`
                      : ''
                  }
                </li>
              `;
            })
            .join('')}
        </ol>
      `;

  const notesHtml = p.notes.length
    ? `
      <section class="panel">
        <h2>Примітки</h2>
        <ul class="clean-list">
          ${p.notes
            .map((n) => {
              const u = safeUrl(n.sourceUrl);
              return `<li>${escapeHtml(n.text)} ${
                u
                  ? `<a href="${escapeHtml(u)}" target="_blank" rel="noopener noreferrer">Джерело ↗</a>`
                  : ''
              }</li>`;
            })
            .join('')}
        </ul>
      </section>
    `
    : '';

  const missingHtml = p.missingData.length
    ? `
      <div class="notice">
        <strong>Чого не знайдено в перевірених джерелах:</strong>
        <ul class="clean-list">
          ${p.missingData.map((m) => `<li>${escapeHtml(m)}</li>`).join('')}
        </ul>
      </div>
    `
    : '';

  const needsCheckHtml = p.needsCheck.length
    ? `
      <div class="notice notice-info">
        <strong>Що потребує додаткової перевірки:</strong>
        <ul class="clean-list">
          ${p.needsCheck.map((m) => `<li>${escapeHtml(m)}</li>`).join('')}
        </ul>
      </div>
    `
    : '';

  const coordBanner = p.hasCoordinates
    ? `<div class="notice${p.coordinatesStatus === 'verified' ? ' notice-ok' : ''}">
         <strong>${escapeHtml(coordinatesStatusLabel(p.coordinatesStatus, true))}</strong>
         (${p.latitude.toFixed(4)}, ${p.longitude.toFixed(4)}).
         ${
           p.coordinatesStatus !== 'verified'
             ? ' Координат на сторінці Prozorro немає; точка додана автоматично або вручну і ще не звірена.'
             : ''
         }
       </div>`
    : `<div class="notice">Координати на мапі відсутні: на сторінці Prozorro координат не вказано.</div>`;

  const verifyBlock = options.showVerifyAction
    ? `
      <div id="verify-panel" style="margin-top:16px;padding-top:14px;border-top:1px solid var(--line);">
        <div style="display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap;">
          <span class="small-note">Перевірка картки адміністратором</span>
          <button type="button" class="button button-sm" id="verify-btn">Позначити перевіреною сьогодні</button>
        </div>
        <p id="verify-msg" class="small-note" aria-live="polite" style="margin:6px 0 0;"></p>
      </div>
    `
    : '';

  return `
    <div class="detail-heading">
      <div class="detail-tags">
        <span class="${statusClass}">${escapeHtml(p.statusLabel)}</span>
        <span class="badge badge-neutral">У Prozorro: ${escapeHtml(p.sourceStatus)}</span>
        <span class="badge badge-info">${escapeHtml(
          executionStatusLabel(p.executionStatus)
        )}</span>
        <span class="badge badge-neutral">ДК ${escapeHtml(p.cpvCode)} · ${escapeHtml(
          p.cpvName
        )}</span>
      </div>
      <h1 class="page-title">${escapeHtml(p.title)}</h1>
      <div class="small-note">
        Ідентифікатор: <strong>${escapeHtml(p.id)}</strong>
        ${p.apiId ? ` · API ID: <code>${escapeHtml(p.apiId)}</code>` : ''}
        ${p.planId ? ` · План: <code>${escapeHtml(p.planId)}</code>` : ' · План: не вказано'}
        ${
          p.verifiedAt
            ? ` · Перевірено: ${escapeHtml(formatDate(p.verifiedAt))}${
                p.verifiedBy ? ` (${escapeHtml(p.verifiedBy)})` : ''
              }`
            : ' · Запис не позначено перевіреним'
        }
      </div>
    </div>

    <div class="detail-grid">
      <div>
        <section class="panel">
          <h2>Основні відомості</h2>
          <dl class="facts">
            <div>
              <dt>Замовник</dt>
              <dd>${escapeHtml(p.buyer)}<br><span class="small-note">ЄДРПОУ ${escapeHtml(
                p.buyerEdrpou
              )}${p.buyerAddress ? ` · ${escapeHtml(p.buyerAddress)}` : ''}</span></dd>
            </div>
            <div>
              <dt>Виконавець / Переможець</dt>
              <dd>${
                p.contractor
                  ? `${escapeHtml(p.contractor)}<br><span class="small-note">ЄДРПОУ ${escapeHtml(
                      p.contractorEdrpou
                    )}</span>`
                  : '<span class="muted">Не визначено (договір не укладено)</span>'
              }</dd>
            </div>
            <div>
              <dt>Очікувана вартість</dt>
              <dd>${formatMoneyWithVatHtml(
                p.expectedAmount,
                p.expectedVatIncluded,
                p.currency
              )}</dd>
            </div>
            <div>
              <dt>Вартість договору</dt>
              <dd>${formatMoneyWithVatHtml(
                p.contractAmount,
                p.contractVatIncluded,
                p.currency
              )}</dd>
            </div>
            <div>
              <dt>Вид предмету і спосіб</dt>
              <dd>${escapeHtml(p.kind)} · ${escapeHtml(p.procurementType)}</dd>
            </div>
            <div>
              <dt>Період поставки / виконання</dt>
              <dd>${escapeHtml(formatPeriod(p.periodStart, p.periodEnd))}</dd>
            </div>
            <div>
              <dt>Місце (${escapeHtml(placeTypeLabel(p.placeType))})</dt>
              <dd>${escapeHtml(p.address || 'На сторінці не вказано')}</dd>
            </div>
            <div>
              <dt>Першоджерело</dt>
              <dd>${
                sourceHref
                  ? `<a href="${escapeHtml(
                      sourceHref
                    )}" target="_blank" rel="noopener noreferrer">Сторінка закупівлі на Prozorro ↗</a>`
                  : 'Немає посилання'
              }</dd>
            </div>
          </dl>
          ${verifyBlock}
        </section>

        ${basisHtml}

        <section class="panel">
          <h2>Фінансування</h2>
          ${fundingHtml}
        </section>

        <section class="panel">
          <h2>Пропозиції учасників</h2>
          ${bidsHtml}
        </section>

        <section class="panel">
          <h2>Хронологія подій</h2>
          ${eventsHtml}
        </section>

        ${notesHtml}
      </div>

      <aside>
        <section class="panel">
          <h2>Місце на мапі</h2>
          ${coordBanner}
          <div id="detail-map" class="map-box"></div>
        </section>

        <section class="panel">
          <h2>Прогалини та перевірка даних</h2>
          ${missingHtml || '<p class="small-note">Прогалин не зафіксовано.</p>'}
          ${needsCheckHtml}
        </section>
      </aside>
    </div>
  `;
}

const detailEl = document.getElementById('detail');
if (detailEl) {
  renderNav('catalog');

  const bannerEl = document.getElementById('detail-mode');
  const id = new URLSearchParams(location.search).get('id');

  async function loadPage() {
    if (!id) {
      detailEl.setAttribute('aria-busy', 'false');
      detailEl.innerHTML =
        '<section class="panel empty-state"><h2>Не вказано ідентифікатор</h2><p><a href="./index.html">Повернутися до каталогу</a></p></section>';
      return;
    }

    await tryRefreshSession();
    const p = await fetchProcurementById(id);
    renderSourceBanner(bannerEl, getSourceInfo());
    detailEl.setAttribute('aria-busy', 'false');

    if (!p) {
      detailEl.innerHTML = `
        <section class="panel empty-state">
          <h2>Закупівлю ${escapeHtml(id)} не знайдено</h2>
          <p><a href="./index.html">Повернутися до каталогу</a></p>
        </section>
      `;
      return;
    }

    document.title = `${p.title} — CivicTrace`;
    const auth = getAuthState();
    const isAdmin = auth.isAuthenticated && auth.roles.includes('Admin');

    detailEl.innerHTML = renderProcurementDetailHtml(p, { showVerifyAction: isAdmin });

    const mapContainer = document.getElementById('detail-map');
    if (mapContainer) {
      const pts = p.hasCoordinates ? [p] : [];
      renderMapWidget(mapContainer, pts, {
        scrollWheelZoom: false,
        zoom: 15,
        openSinglePopup: true
      });
    }

    const verifyBtn = document.getElementById('verify-btn');
    const verifyMsg = document.getElementById('verify-msg');
    verifyBtn?.addEventListener('click', async () => {
      verifyBtn.disabled = true;
      verifyMsg.textContent = 'Зберігаємо…';
      try {
        await verifyProcurement(p.id, null);
        verifyMsg.textContent = 'Позначено перевіреною. Оновлюємо картку…';
        await loadPage();
      } catch (err) {
        verifyMsg.textContent = `Помилка: ${err.message}`;
        verifyBtn.disabled = false;
      }
    });
  }

  loadPage();
}