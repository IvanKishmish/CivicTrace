const searchable = value => (value ?? '').trim().toLocaleLowerCase('uk');

export function filterCatalog(items, { query, category, status }) {
  const search = searchable(query);
  return items.filter(item =>
    (!search || [item.title, item.buyer, item.externalId].some(value => searchable(value).includes(search)))
    && (!category || item.category === category)
    && (!status || item.status === status));
}

export function populateFilter(select, items, field) {
  select.length = 1;
  const values = [...new Set(items.map(item => item[field]).filter(Boolean))];
  values.sort((a, b) => a.localeCompare(b, 'uk'));
  for (const value of values) select.add(new Option(value, value));
  select.disabled = values.length === 0;
}
