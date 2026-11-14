export const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
export function money(value, currency = 'UAH') {
  if (typeof value !== 'number' || !Number.isFinite(value)) return 'Сума невідома';
  try { return new Intl.NumberFormat('uk-UA', {style:'currency', currency:currency || 'UAH',maximumFractionDigits:2}).format(value); } catch { return `${value} (валюта невідома)`; }
}
export function date(value) {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return 'Дата невідома';
  const parsed = new Date(`${value}T12:00:00Z`);
  return Number.isNaN(parsed.getTime()) ? 'Дата невідома' : new Intl.DateTimeFormat('uk-UA',{timeZone:'UTC'}).format(parsed);
}
export function safeLink(url, label = 'Переглянути джерело ↗') {
  try { const parsed = new URL(url); if (!['https:','http:'].includes(parsed.protocol)) throw Error(); return `<a href="${escapeHtml(parsed.href)}" target="_blank" rel="noopener noreferrer">${escapeHtml(label)}</a>`; } catch { return '<span class="muted">Джерело не надано</span>'; }
}
export async function loadProcurements() {
  const response = await fetch('./mock-procurements.json');
  if (!response.ok) throw new Error('Не вдалося завантажити дані');
  const data = await response.json();
  if (!Array.isArray(data) || data.some(item => !item || typeof item !== 'object' || typeof item.id !== 'string') || new Set(data.map(item=>item.id)).size !== data.length) throw new Error('Некоректний формат даних');
  return data;
}
