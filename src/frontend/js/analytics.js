import { downloadExport, fetchAnalytics, getSourceInfo } from './api.js';
import { formatMoney } from './format.js';
import { escapeHtml, renderNav, renderSourceBanner } from './nav.js';

renderNav('analytics');

const bannerEl = document.getElementById('analytics-mode');
const groupTabsEl = document.getElementById('group-tabs');
const summaryEl = document.getElementById('analytics-summary');
const chartEl = document.getElementById('analytics-chart');
const tableHeadEl = document.getElementById('analytics-table-head');
const tableBodyEl = document.getElementById('analytics-table-body');
const notesEl = document.getElementById('analytics-notes');

const GROUP_OPTIONS = [
  { key: 'buyer', label: 'За замовником' },
  { key: 'cpv', label: 'За кодом ДК' },
  { key: 'year', label: 'За роком' },
  { key: 'method', label: 'За способом проведення' },
  { key: 'status', label: 'За статусом' },
  { key: 'funding', label: 'За джерелом фінансування' },
  { key: 'expenditure', label: 'За кодом видатків' }
];

let currentGroupBy = new URLSearchParams(location.search).get('groupBy') || 'buyer';
if (!GROUP_OPTIONS.some((o) => o.key === currentGroupBy)) currentGroupBy = 'buyer';

function renderTabs() {
  groupTabsEl.innerHTML = GROUP_OPTIONS.map(
    (opt) => `
      <button type="button"
        class="button button-sm ${opt.key === currentGroupBy ? '' : 'button-secondary'}"
        data-group="${opt.key}"
        aria-pressed="${opt.key === currentGroupBy}">
        ${opt.label}
      </button>
    `
  ).join('');

  groupTabsEl.querySelectorAll('[data-group]').forEach((btn) => {
    btn.addEventListener('click', () => {
      currentGroupBy = btn.getAttribute('data-group');
      const next = `${location.pathname}?groupBy=${encodeURIComponent(currentGroupBy)}`;
      history.replaceState(null, '', next);
      renderTabs();
      loadAnalytics();
    });
  });
}

function renderSummary(s) {
  const avgBidsText =
    s.averageBidsInCompleteLists !== null && s.averageBidsInCompleteLists !== undefined
      ? `${s.averageBidsInCompleteLists.toFixed(1)} (лише для ${s.bidsListCompleteCount} із ${s.procurementsCount})`
      : 'Немає повних реєстрів';

  summaryEl.innerHTML = `
    <div class="panel kpi-card">
      <span>Усього закупівель</span>
      <strong>${s.procurementsCount}</strong>
      <div class="small-note">Договір укладено: ${s.contractSignedCount} · Не відбулися: ${s.notHeldCount}</div>
    </div>
    <div class="panel kpi-card">
      <span>Без електронної системи</span>
      <strong>${s.noElectronicSystemCount}</strong>
      <div class="small-note">Спосіб проведення процедури, не висновок про порушення</div>
    </div>
    <div class="panel kpi-card">
      <span>Середня к-ть пропозицій</span>
      <strong>${escapeHtml(avgBidsText)}</strong>
      <div class="small-note">Лише для закупівель із повним реєстром пропозицій (${s.bidsListCompleteCount} із ${s.procurementsCount})</div>
    </div>
    <div class="panel kpi-card">
      <span>Повнота оприлюднених даних</span>
      <strong>${s.recordsWithMissingData} із ${s.procurementsCount}</strong>
      <div class="small-note">Із прогалинами (сер. ${s.averageMissingDataItems.toFixed(2)}/запис) · Потребують перевірки: ${s.recordsNeedingCheck}</div>
    </div>

    <div class="panel kpi-card" style="grid-column: span 2;">
      <span>Очікувана вартість (суми окремо за ПДВ, не складаються)</span>
      <div style="margin-top:8px;display:flex;gap:14px;flex-wrap:wrap;font-size:13px;">
        <div><span class="vat-tag" style="margin-left:0;">з ПДВ</span> <strong>${escapeHtml(formatMoney(s.expectedAmount.withVat))}</strong></div>
        <div><span class="vat-tag" style="margin-left:0;">без ПДВ</span> <strong>${escapeHtml(formatMoney(s.expectedAmount.withoutVat))}</strong></div>
        <div><span class="vat-tag" style="margin-left:0;">ПДВ не вказано</span> <strong>${escapeHtml(formatMoney(s.expectedAmount.vatUnspecified))}</strong></div>
      </div>
    </div>

    <div class="panel kpi-card" style="grid-column: span 2;">
      <span>Вартість укладених договорів (окремо за ПДВ, не складаються)</span>
      <div style="margin-top:8px;display:flex;gap:14px;flex-wrap:wrap;font-size:13px;">
        <div><span class="vat-tag" style="margin-left:0;">з ПДВ</span> <strong>${escapeHtml(formatMoney(s.contractAmount.withVat))}</strong></div>
        <div><span class="vat-tag" style="margin-left:0;">без ПДВ</span> <strong>${escapeHtml(formatMoney(s.contractAmount.withoutVat))}</strong></div>
        <div><span class="vat-tag" style="margin-left:0;">ПДВ не вказано</span> <strong>${escapeHtml(formatMoney(s.contractAmount.vatUnspecified))}</strong></div>
      </div>
    </div>
  `;
}

function renderChart(groups) {
  if (!groups.length) {
    chartEl.innerHTML = '<p class="muted">Немає даних для діаграми.</p>';
    return;
  }
  const maxCount = Math.max(...groups.map((g) => g.procurementsCount), 1);

  chartEl.innerHTML = groups
    .map((g) => {
      const pct = Math.max(4, Math.round((g.procurementsCount / maxCount) * 100));
      return `
        <div style="margin-bottom:12px;">
          <div style="display:flex;justify-content:space-between;gap:8px;font-size:13px;margin-bottom:4px;">
            <span><strong>${escapeHtml(g.label)}</strong> <span class="muted">(${escapeHtml(g.key)})</span></span>
            <span class="num"><strong>${g.procurementsCount}</strong> (договори: ${g.contractSignedCount})</span>
          </div>
          <div style="background:#edf0e9;border-radius:6px;height:12px;overflow:hidden;">
            <div style="width:${pct}%;height:100%;background:var(--accent);border-radius:6px;"></div>
          </div>
        </div>
      `;
    })
    .join('');
}

function renderTable(groups, showFunding) {
  tableHeadEl.innerHTML = `
    <tr>
      <th>Група</th>
      <th class="num">Закупівель</th>
      <th class="num">Договір</th>
      <th class="num">Не відбулися</th>
      <th class="num">Без ел. системи</th>
      <th class="num">Договори (з ПДВ)</th>
      <th class="num">Договори (без ПДВ)</th>
      <th class="num">Договори (ПДВ не вказ.)</th>
      ${showFunding ? '<th class="num">Сума фінансування</th>' : ''}
      <th class="num">Із прогалинами</th>
    </tr>
  `;

  tableBodyEl.innerHTML = groups
    .map(
      (g) => `
      <tr>
        <td>
          <strong>${escapeHtml(g.label)}</strong><br>
          <span class="small-note"><code>${escapeHtml(g.key)}</code></span>
        </td>
        <td class="num"><strong>${g.procurementsCount}</strong></td>
        <td class="num">${g.contractSignedCount}</td>
        <td class="num">${g.notHeldCount}</td>
        <td class="num">${g.noElectronicSystemCount}</td>
        <td class="num">${escapeHtml(formatMoney(g.contractAmount.withVat))}</td>
        <td class="num">${escapeHtml(formatMoney(g.contractAmount.withoutVat))}</td>
        <td class="num">${escapeHtml(formatMoney(g.contractAmount.vatUnspecified))}</td>
        ${
          showFunding
            ? `<td class="num">${
                g.fundingAmount !== null ? escapeHtml(formatMoney(g.fundingAmount)) : '—'
              }</td>`
            : ''
        }
        <td class="num">${g.recordsWithMissingData}</td>
      </tr>
    `
    )
    .join('');
}

async function loadAnalytics() {
  const data = await fetchAnalytics(currentGroupBy);
  renderSourceBanner(bannerEl, getSourceInfo());
  renderSummary(data.summary);
  renderChart(data.groups);
  renderTable(data.groups, currentGroupBy === 'funding' || currentGroupBy === 'expenditure');

  notesEl.innerHTML = (data.notes || [])
    .map((n) => `<li>${escapeHtml(n)}</li>`)
    .join('');
}

document.getElementById('export-csv')?.addEventListener('click', () => downloadExport('csv'));
document.getElementById('export-json')?.addEventListener('click', () => downloadExport('json'));

renderTabs();
loadAnalytics();