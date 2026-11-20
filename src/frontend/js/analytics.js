import { exportRentalsUrl, fetchRentalAnalytics } from './api.js';
import { formatArea, formatMoney } from './format.js';
import { escapeHtml, renderLastUpdatedBanner, renderNav } from './nav.js';

renderNav('analytics');

const bannerEl = document.getElementById('analytics-mode');
const summaryEl = document.getElementById('analytics-summary');
const chartsEl = document.getElementById('analytics-charts');
const rulesBody = document.getElementById('rules-body');
const riskObjectsBody = document.getElementById('risk-objects-body');
const unenrichedBody = document.getElementById('unenriched-body');

function barBlock(title, items) {
  const max = Math.max(...items.map((i) => i.count), 1);
  return `
    <section class="panel" style="padding:20px;">
      <h2 style="margin-top:0;font-size:18px;">${escapeHtml(title)}</h2>
      ${items.map((i) => `
        <div style="margin-bottom:10px;">
          <div style="display:flex;justify-content:space-between;font-size:13px;margin-bottom:3px;">
            <span><strong>${escapeHtml(i.label)}</strong></span>
            <span>${i.count} шт. (${escapeHtml(formatArea(i.totalAreaSqm))})</span>
          </div>
          <div style="background:#edf0e9;border-radius:6px;height:10px;">
            <div style="width:${Math.max(3, Math.round((i.count / max) * 100))}%;height:100%;background:var(--accent);border-radius:6px;"></div>
          </div>
        </div>
      `).join('') || '<p class="muted">Показники з’являться після отримання даних із джерела.</p>'}
    </section>
  `;
}

async function load() {
  const a = await fetchRentalAnalytics();
  renderLastUpdatedBanner(bannerEl, a.lastUpdatedAt, `Дані Prozorro.Продажі знайдено для ${a.enrichedCount} із ${a.totalObjects} записів`);

  summaryEl.innerHTML = `
    <div class="panel kpi-card"><span>Записів у переліку міста</span><strong>${a.totalObjects}</strong><div class="small-note">Зіставлено з Prozorro.Продажі: ${a.enrichedCount} · Без пов’язаних аукціонів: ${a.objectsWithoutAuctionsCount}</div></div>
    <div class="panel kpi-card"><span>Відома площа об’єктів зі статусом «вільне»</span><strong>${escapeHtml(formatArea(a.freeAreaSqm))}</strong><div class="small-note">Сума відомих площ вільних записів, не всього майна громади</div></div>
    <div class="panel kpi-card"><span>Медіана ставки аукціонів</span><strong>${a.medianRatePerSqmMonth != null ? escapeHtml(formatMoney(a.medianRatePerSqmMonth)) + '/м²' : 'Очікує аукціонів'}</strong><div class="small-note">Орендних аукціонів у БД: ${a.auctionsCount}</div></div>
    <div class="panel kpi-card"><span>Орієнтир потенційного річного доходу</span><strong>${a.estimatedLostAnnualIncomeUah != null ? escapeHtml(formatMoney(a.estimatedLostAnnualIncomeUah)) + '/рік' : 'Немає даних для оцінки'}</strong><div class="small-note">Груба оцінка: відома вільна площа × медіанна ставка аукціону × 12. Це не фактичний дохід і не прогноз бюджету.</div></div>
  `;

  chartsEl.innerHTML = `
    ${barBlock('За статусом', a.byStatus)}
    ${barBlock('За роком внесення до міського переліку', a.byYear)}
    ${barBlock('За типом об’єкта', a.byObjectType)}
    ${barBlock('Сигнали, що спрацювали', a.byRiskFlag)}
  `;

  rulesBody.innerHTML = a.riskRules.map((r) => `
    <tr>
      <td><code>${escapeHtml(r.code)}</code><br><strong>${escapeHtml(r.title)}</strong></td>
      <td class="num">${r.weight}</td>
      <td><code>${escapeHtml(r.formula)}</code><br><span class="small-note">Поріг: ${escapeHtml(r.threshold)}</span></td>
      <td>${escapeHtml(r.whyRisk)}</td>
      <td>${escapeHtml(r.legitimateExplanation)}</td>
    </tr>
  `).join('');

  riskObjectsBody.innerHTML = a.topRiskObjects.map((o) => `
    <tr>
      <td><a href="./rental.html?id=${encodeURIComponent(o.ocid)}"><code>${escapeHtml(o.ocid)}</code></a><br><span class="small-note">${escapeHtml(o.title)}</span></td>
      <td>${escapeHtml(o.statusLabel)}</td>
      <td class="num">${escapeHtml(formatArea(o.areaSqm))}</td>
      <td class="num"><span class="badge badge-risk">${o.riskScore}</span></td>
      <td>${o.riskFlags.map((f) => `<span class="badge badge-warn" style="margin:2px;">${escapeHtml(f.title)}</span>`).join(' ')}</td>
    </tr>
  `).join('') || '<tr><td colspan="5" class="muted">Поки немає об’єктів зі спрацьованими сигналами ризику.</td></tr>';

  unenrichedBody.innerHTML = a.unenrichedSample.map((o) => `
    <tr>
      <td><a href="./rental.html?id=${encodeURIComponent(o.ocid)}"><code>${escapeHtml(o.ocid)}</code></a></td>
      <td>${escapeHtml(o.rawTitle)}</td>
      <td>${escapeHtml(o.dataDefectsNotes || 'Запис міського списку не знайдено в API')}</td>
    </tr>
  `).join('') || '<tr><td colspan="3" class="muted">Для всіх записів знайдено відповідні дані в Prozorro.Продажі.</td></tr>';
}

document.getElementById('export-csv')?.addEventListener('click', () => { location.href = exportRentalsUrl('csv'); });
document.getElementById('export-json')?.addEventListener('click', () => { location.href = exportRentalsUrl('json'); });
async function refresh() {
  bannerEl.textContent = 'Оновлюємо аналітику…';
  try { await load(); }
  catch (error) {
    bannerEl.textContent = 'Аналітика тимчасово недоступна.';
    summaryEl.innerHTML = `<div class="panel empty-state"><p>${escapeHtml(error.message)}</p><button id="retry-analytics" type="button" class="button">Спробувати ще раз</button></div>`;
    document.getElementById('retry-analytics').addEventListener('click', refresh);
  }
}
refresh();
