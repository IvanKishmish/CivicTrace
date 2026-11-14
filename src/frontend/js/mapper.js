export const unknown = 'Не знайдено у перевірених джерелах';

function text(value) {
  return typeof value === 'string' && value.trim() ? value.trim() : null;
}

function amount(value) {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

function source(value) {
  try {
    const url = new URL(value);
    if (['http:', 'https:'].includes(url.protocol)) return url.href;
  } catch {
    return null;
  }
  return null;
}

const statuses = {
  active: 'Процедура триває',
  complete: 'Процедуру завершено',
  completed: 'Процедуру завершено',
  cancelled: 'Процедуру скасовано',
  unsuccessful: 'Торги не відбулися',
  'Завершена': 'Процедуру завершено',
};

export function mapProcurement(item) {
  const externalId = text(item?.externalId) ?? text(item?.id);
  if (!externalId) throw new Error('Запис не має ідентифікатора');

  const status = text(item.status);
  return {
    externalId,
    title: text(item.title),
    buyer: text(typeof item.buyer === 'object' ? item.buyer?.name : item.buyer),
    category: text(item.category),
    status: Object.hasOwn(statuses, status) ? statuses[status] : status,
    expectedAmount: amount(item.expectedAmount),
    contractAmount: amount(item.contractAmount),
    currency: text(item.currency),
    sourceUrl: source(item.sourceUrl),
  };
}

export function formatAmount(value, currency) {
  if (value === null) return unknown;
  const options = /^[A-Z]{3}$/.test(currency)
    ? { style: 'currency', currency }
    : { maximumFractionDigits: 2 };
  return new Intl.NumberFormat('uk-UA', options).format(value);
}
