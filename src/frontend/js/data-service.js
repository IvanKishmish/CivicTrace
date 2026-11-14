import { catalogConfig } from './config.js';
import { mapProcurement } from './mapper.js';

async function readJson(url, signal) {
  const response = await fetch(url, { signal });
  if (!response.ok) throw Object.assign(new Error('HTTP ' + response.status), { status: response.status });
  return response.json();
}

function readPage(page, expectedNumber) {
  if (!Array.isArray(page?.items)
      || page.pageNumber !== expectedNumber
      || !Number.isInteger(page.pageSize) || page.pageSize < 1
      || !Number.isInteger(page.totalCount) || page.totalCount < 0
      || !Number.isInteger(page.totalPages) || page.totalPages < 0
      || page.items.length > page.pageSize
      || page.totalPages !== Math.ceil(page.totalCount / page.pageSize)) {
    throw new Error('Некоректна відповідь пагінації');
  }
  return page;
}

async function loadApi(signal) {
  const url = new URL('/api/v1/procurements', catalogConfig.apiBaseUrl);
  url.searchParams.set('pageSize', catalogConfig.pageSize);
  url.searchParams.set('pageNumber', 1);
  const first = readPage(await readJson(url, signal), 1);
  const items = [...first.items];
  for (let number = 2; number <= first.totalPages; number++) {
    url.searchParams.set('pageNumber', number);
    const page = readPage(await readJson(url, signal), number);
    if (page.totalCount !== first.totalCount || page.pageSize !== first.pageSize) {
      throw new Error('Набір даних змінився під час завантаження');
    }
    items.push(...page.items);
  }
  if (items.length !== first.totalCount) throw new Error('Неповна відповідь API');
  return normalize(items);
}

function normalize(items) {
  if (!Array.isArray(items)) throw new Error('Очікувався масив записів');
  const mapped = items.map(mapProcurement);
  if (new Set(mapped.map(item => item.externalId)).size !== mapped.length) {
    throw new Error('Повторювані ідентифікатори');
  }
  return mapped;
}

export async function loadCatalog() {
  let apiFailed = false;
  if (catalogConfig.mode === 'api') {
    try {
      const items = await loadApi(AbortSignal.timeout(8000));
      return { items, origin: 'api', apiFailed };
    } catch {
      apiFailed = true;
    }
  }
  try {
    const items = normalize(await readJson(catalogConfig.staticUrl, AbortSignal.timeout(5000)));
    return { items, origin: 'static', apiFailed };
  } catch (cause) {
    const message = apiFailed
      ? 'API недоступне, а початковий набір не завантажився.'
      : 'Початковий набір не завантажився.';
    throw new Error(message, { cause });
  }
}

export async function loadProcurement(id) {
  if (!id) return null;
  if (catalogConfig.mode === 'api') {
    const endpoint = '/api/v1/procurements/' + encodeURIComponent(id);
    try {
      return await readJson(new URL(endpoint, catalogConfig.apiBaseUrl), AbortSignal.timeout(8000));
    } catch (error) {
      if (error.status === 404) return null;
    }
  }
  const items = await readJson(catalogConfig.staticUrl, AbortSignal.timeout(5000));
  if (!Array.isArray(items)) throw new Error('Некоректний початковий набір');
  return items.find(item => (item.externalId ?? item.id) === id) ?? null;
}
