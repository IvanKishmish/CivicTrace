import { loadCatalog } from './data-service.js';
import { filterCatalog, populateFilter } from './filters.js';
import { formatAmount, unknown } from './mapper.js';

const form = document.querySelector('#filters');
const results = document.querySelector('#results');
const count = document.querySelector('#result-count');
const banner = document.querySelector('#catalog-mode');
const controls = form.querySelector('fieldset');
let items = [];
let active = false;

function element(tag, text, className) {
  const node = document.createElement(tag);
  if (text != null) node.textContent = text;
  if (className) node.className = className;
  return node;
}

function amountFact(label, value, currency) {
  const node = element('div');
  node.append(element('span', label, 'muted'), element('strong', formatAmount(value, currency)));
  return node;
}

function card(item) {
  const node = element('article', null, 'panel procurement-card');
  const top = element('div', null, 'card-top');
  top.append(element('span', item.category ?? unknown, 'eyebrow'),
    element('span', item.status ?? unknown, 'badge'));

  const title = element('h3');
  const detail = element('a', item.title ?? unknown);
  detail.href = './procurement.html?' + new URLSearchParams({
    id: item.externalId,
    externalId: item.externalId,
  });
  title.append(detail);

  const facts = element('div', null, 'card-facts');
  facts.append(amountFact('Очікувана вартість', item.expectedAmount, item.currency),
    amountFact('Сума договору', item.contractAmount, item.currency));

  const footer = element('div', null, 'card-bottom');
  footer.append(element('span', 'Джерело', 'muted'));
  if (item.sourceUrl) {
    const label = 'Відкрити джерело ↗';
    const link = element('a', label);
    link.href = item.sourceUrl;
    link.target = '_blank';
    link.rel = 'noopener noreferrer';
    footer.append(link);
  } else {
    footer.append(element('span', unknown, 'muted'));
  }

  node.append(top, title, element('p', item.externalId, 'muted record-id'),
    element('p', item.buyer ?? unknown, 'buyer'), facts, footer);
  return node;
}

function render() {
  if (!active) return;
  const { q, category, status } = form.elements;
  const shown = filterCatalog(items, { query: q.value, category: category.value, status: status.value });
  count.textContent = 'Знайдено: ' + shown.length + ' із ' + items.length;
  results.replaceChildren(...shown.map(card));
  if (!shown.length) {
    const empty = element('div', null, 'panel empty-state');
    const message = items.length ? 'Змініть запит або скиньте фільтри.' : 'У джерелі поки немає записів.';
    empty.append(element('h3', 'Нічого не знайдено'), element('p', message));
    results.append(empty);
  }
  const params = new URLSearchParams();
  for (const [key, value] of new FormData(form)) if (value) params.set(key, value);
  const query = params.toString();
  history.replaceState(null, '', location.pathname + (query ? '?' + query : ''));
}

function retryButton() {
  const button = element('button', 'Спробувати ще раз', 'button button-secondary');
  button.type = 'button';
  button.addEventListener('click', init);
  return button;
}

async function init() {
  active = false;
  controls.disabled = true;
  banner.replaceChildren();
  results.replaceChildren();
  results.setAttribute('aria-busy', 'true');
  count.textContent = 'Завантажуємо закупівлі…';
  try {
    const catalog = await loadCatalog();
    items = catalog.items;
    const message = catalog.apiFailed
      ? 'API недоступне. Показуємо початковий набір CivicTrace.'
      : catalog.origin === 'static' ? 'Джерело каталогу · Початковий набір CivicTrace' : 'Джерело каталогу · API CivicTrace';
    banner.append(element('span', message));
    if (catalog.apiFailed) banner.append(retryButton());
    populateFilter(form.elements.category, items, 'category');
    populateFilter(form.elements.status, items, 'status');
    const params = new URLSearchParams(location.search);
    for (const key of ['q', 'category', 'status']) {
      const input = form.elements[key];
      input.value = params.get(key) ?? '';
      if (input.selectedIndex === -1) input.selectedIndex = 0;
    }
    active = true;
    controls.disabled = false;
    render();
  } catch (error) {
    count.textContent = 'Дані недоступні';
    const panel = element('div', null, 'panel empty-state');
    panel.setAttribute('role', 'alert');
    panel.append(element('h3', 'Не вдалося завантажити каталог'), element('p', error.message), retryButton());
    results.replaceChildren(panel);
  } finally {
    results.setAttribute('aria-busy', 'false');
  }
}

form.addEventListener('input', render);
form.addEventListener('submit', event => {
  event.preventDefault();
  render();
});
form.addEventListener('reset', () => setTimeout(render, 0));
init();
