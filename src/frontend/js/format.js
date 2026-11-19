import { escapeHtml } from './nav.js';

export function safeUrl(url) {
  if (!url || typeof url !== 'string') return null;
  try {
    const parsed = new URL(url, location.origin);
    if (parsed.protocol === 'http:' || parsed.protocol === 'https:') return parsed.href;
  } catch { }
  return null;
}

export function formatMoney(amount, currency = 'UAH') {
  if (amount === null || amount === undefined || Number.isNaN(Number(amount))) return 'Не вказано';
  const num = new Intl.NumberFormat('uk-UA', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(amount));
  return currency === 'UAH' ? `${num} ₴` : `${num} ${escapeHtml(currency)}`;
}

export function formatArea(area) {
  if (area === null || area === undefined || Number.isNaN(Number(area))) return 'Не вказано';
  return `${new Intl.NumberFormat('uk-UA', { minimumFractionDigits: 1, maximumFractionDigits: 2 }).format(Number(area))} м²`;
}

export function formatDate(iso) {
  if (!iso) return 'Не вказано';
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(iso));
  return m ? `${m[3]}.${m[2]}.${m[1]}` : String(iso);
}

export function listTypeLabel(code) {
  if (code === 'first') return '1-й тип (аукціон)';
  if (code === 'second') return '2-й тип (без аукціону)';
  return 'Тип переліку не визначено';
}

export function statusBadgeClass(code) {
  if (code === 'free') return 'badge';
  if (code === 'on_auction') return 'badge badge-info';
  if (code === 'rented') return 'badge badge-neutral';
  return 'badge badge-warn';
}

export function coordStatusText(status, hasCoords) {
  if (!hasCoords) return 'Координати відсутні';
  if (status === 'VerifiedFromApi') return 'Координати з Prozorro.Продажі';
  return 'Геокодовано за адресою (потребує перевірки)';
}