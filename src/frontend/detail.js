import { escapeHtml, money, date, safeLink } from './data.js';
import { loadProcurement } from './js/data-service.js';

const root = document.querySelector('#detail');
const text = value => value === null || value === undefined || value === '' ? '<span class="muted">Немає даних</span>' : escapeHtml(String(value));
const fact = (label, value) => `<div><dt>${label}</dt><dd>${value}</dd></div>`;

function timeline(events) {
  if (!Array.isArray(events) || events.length === 0) return '<p class="muted">Події ще не додано. Часова шкала з’явиться, коли будуть доступні дані з джерел.</p>';
  const sorted = [...events].sort((a, b) => String(a?.date ?? '9999').localeCompare(String(b?.date ?? '9999')));
  return `<ol class="timeline">${sorted.map(event => `<li><div class="timeline-date">${date(event?.date)}</div><h3>${text(event?.title)}</h3><div class="timeline-source">${safeLink(event?.sourceUrl, 'Джерело події ↗')}</div></li>`).join('')}</ol>`;
}

function render(item) {
  document.title = `${item.title || 'Картка закупівлі'} — CivicTrace`;
  const notices = [];
  if (item.contractAmount === null || item.contractAmount === undefined) notices.push('Сума договору відсутня.');
  if (!item.contractor) notices.push('Постачальника не зазначено.');
  if (!item.periodStart || !item.periodEnd) notices.push('Не всі дати доступні.');
  if (!item.sourceUrl) notices.push('Посилання на першоджерело відсутнє.');
  if (!item.hasCoordinates || !Number.isFinite(item.latitude) || !Number.isFinite(item.longitude)) notices.push('Координати не підтверджені; об’єкт не позначено на мапі.');
  root.innerHTML = `
    <section class="detail-heading">
      <p class="eyebrow">КАРТКА ЗАКУПІВЛІ · ${text(item.id)}</p>
      <h1 class="page-title">${text(item.title)}</h1>
      <div class="detail-tags"><span class="badge">${text(item.statusLabel ?? item.status)}</span><span class="muted">${text(item.cpvName ?? item.kind)}</span></div>
      <p class="muted">Стан процедури та оприлюднені події не підтверджують фактичне виконання робіт.</p>
    </section>
    <div class="detail-grid">
      <div>
        <section class="panel" aria-labelledby="facts-title">
          <p class="eyebrow">ОСНОВНІ ВІДОМОСТІ</p><h2 id="facts-title">Що відомо про закупівлю</h2>
          <dl class="facts">
            ${fact('Очікувана вартість', escapeHtml(money(item.expectedAmount, item.currency)))}
            ${fact('Сума договору', escapeHtml(money(item.contractAmount, item.currency)))}
            ${fact('Замовник', text(item.buyer))}
            ${fact('ЄДРПОУ замовника', text(item.buyerEdrpou))}
            ${fact('Постачальник', text(item.contractor))}
            ${fact('ЄДРПОУ постачальника', text(item.contractorEdrpou))}
            ${fact('Тип процедури', text(item.procurementType))}
            ${fact('Адреса з даних', text(item.address))}
            ${fact('Дата початку з даних', date(item.periodStart))}
            ${fact('Дата закінчення з даних', date(item.periodEnd))}
          </dl>
        </section>
        <section class="panel" aria-labelledby="timeline-title"><p class="eyebrow">ОПРИЛЮДНЕНІ ПОДІЇ</p><h2 id="timeline-title">Часова шкала</h2><p class="muted">Показуємо лише наявні події. Не додаємо припущень про наступні етапи.</p>${timeline(item.events)}</section>
      </div>
      <aside>
        <section class="panel" aria-labelledby="sources-title"><p class="eyebrow">ПЕРЕВІРИТИ ВІДОМОСТІ</p><h2 id="sources-title">Джерела</h2><p>${safeLink(item.sourceUrl, 'Відкрити першоджерело ↗')}</p><p class="muted">Оригінальний запис закупівлі в Prozorro.</p></section>
        <section class="panel notice" aria-labelledby="gaps-title"><p class="eyebrow">ПОВНОТА ДАНИХ</p><h2 id="gaps-title">Що потребує уточнення</h2>${notices.length ? `<ul>${notices.map(notice => `<li>${notice}</li>`).join('')}</ul>` : '<p>Для основних полів цього запису пропусків не виявлено.</p>'}${item.missingData?.length ? `<p>${text(item.missingData.join(' · '))}</p>` : ''}<p class="muted">«Немає даних» означає відсутність відомостей у наборі, а не відсутність події.</p></section>
      </aside>
    </div>`;
}

async function init() {
  root.setAttribute('aria-busy', 'true');
  try {
    const id = new URLSearchParams(location.search).get('id');
    const item = await loadProcurement(id);

    if (!item) {
      document.title = 'Картку не знайдено — CivicTrace';
      root.innerHTML = '<section class="panel"><p class="eyebrow">КАРТКУ НЕ ЗНАЙДЕНО</p><h1>Закупівлю не знайдено</h1><p class="muted">Перевірте посилання або виберіть закупівлю в каталозі.</p><a class="button" href="index.html">До каталогу</a></section>';
      return;
    }
    render(item);
  } catch {
    root.innerHTML = '<section class="panel"><h1>Не вдалося завантажити дані</h1><p>Перевірте з’єднання та відкрийте сайт через локальний HTTP-сервер за інструкцією в README.</p><button class="button" id="retry" type="button">Спробувати ще раз</button></section>';
    root.querySelector('#retry').addEventListener('click', init);
  } finally {
    root.setAttribute('aria-busy', 'false');
  }
}

init();
