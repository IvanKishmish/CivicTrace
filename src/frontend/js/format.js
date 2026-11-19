import { escapeHtml } from './nav.js';

export function safeUrl(url) {
  if (!url || typeof url !== 'string') return null;
  try {
    const parsed = new URL(url, location.origin);
    if (parsed.protocol === 'http:' || parsed.protocol === 'https:') {
      return parsed.href;
    }
  } catch {
    return null;
  }
  return null;
}

export function formatMoney(amount, currency = 'UAH') {
  if (amount === null || amount === undefined || Number.isNaN(Number(amount))) {
    return 'Не вказано';
  }
  const formatted = new Intl.NumberFormat('uk-UA', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(Number(amount));

  return currency === 'UAH' ? `${formatted} ₴` : `${formatted} ${escapeHtml(currency)}`;
}

export function vatLabel(vatIncluded) {
  if (vatIncluded === true) return 'з ПДВ';
  if (vatIncluded === false) return 'без ПДВ';
  return 'ПДВ не вказано';
}

export function formatMoneyWithVatHtml(amount, vatIncluded, currency = 'UAH') {
  if (amount === null || amount === undefined || Number.isNaN(Number(amount))) {
    return '<span class="muted">Не вказано</span>';
  }
  return `<strong>${escapeHtml(formatMoney(amount, currency))}</strong><span class="vat-tag">${escapeHtml(
    vatLabel(vatIncluded)
  )}</span>`;
}

export function formatDate(isoDate) {
  if (!isoDate) return 'Дату не вказано';
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(isoDate));
  if (!match) return String(isoDate);
  return `${match[3]}.${match[2]}.${match[1]}`;
}

export function formatPeriod(start, end) {
  if (!start && !end) return 'Не вказано';
  if (start && end) return `${formatDate(start)} — ${formatDate(end)}`;
  return start ? `від ${formatDate(start)}` : `до ${formatDate(end)}`;
}

export function placeTypeLabel(code) {
  switch (code) {
    case 'work':
      return 'Місце робіт';
    case 'delivery':
      return 'Місце поставки';
    default:
      return 'Місце не вказано';
  }
}

export function executionStatusLabel(code) {
  switch (code) {
    case 'not_confirmed':
      return 'Фактичне виконання не підтверджено';
    case 'not_applicable':
      return 'Договору немає (виконання не застосовується)';
    default:
      return 'Стан виконання невідомий';
  }
}

export function coordinatesStatusLabel(status, hasCoordinates) {
  if (!hasCoordinates) return 'Координати відсутні';
  if (status === 'verified') return 'Координати звірено';
  return 'Координати потребують перевірки';
}

const STAGE_LABELS = {
  announced: 'Оголошення',
  protocol_published: 'Протокол рішення',
  clarification_deadline: 'Строк роз’яснень',
  bids_deadline: 'Кінцевий строк подання',
  bids_opened: 'Розкриття пропозицій',
  bid_rejected: 'Пропозицію відхилено',
  award_notice: 'Намір укласти договір',
  reported: 'Звіт / протокол розкриття',
  contract_signed: 'Договір укладено',
  cancelled: 'Відміна закупівлі'
};

export function stageLabel(stage) {
  return STAGE_LABELS[stage] || stage || 'Подія';
}

export function bidOutcomeLabel(outcome) {
  if (outcome === 'winner') return 'Переможець';
  if (outcome === 'rejected') return 'Відхилено';
  return 'Не зазначено';
}

export function kindCodeFromLabel(label) {
  if (label === 'Товари') return 'goods';
  if (label === 'Послуги') return 'services';
  if (label === 'Роботи') return 'works';
  return '';
}

export function methodCodeFromLabel(label) {
  if (label === 'Закупівля без використання електронної системи') return 'no_electronic_system';
  if (label === 'Запит (ціни) пропозицій') return 'request_for_quotations';
  if (label === 'Відкриті торги з особливостями') return 'open_tenders_with_specifics';
  if (label === 'Інша процедура') return 'other';
  return '';
}

export function firstEventYear(procurement) {
  const years = (procurement.events || [])
    .map((e) => e.date)
    .filter(Boolean)
    .map((d) => Number(String(d).slice(0, 4)))
    .filter((y) => !Number.isNaN(y));
  if (years.length === 0) return null;
  return Math.min(...years);
}