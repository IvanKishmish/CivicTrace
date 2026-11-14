export const catalogConfig = {
  mode: 'static',
  apiBaseUrl: 'http://localhost:5175',
  staticUrl: new URL('../mock-procurements.json', import.meta.url),
  pageSize: 20,
};
