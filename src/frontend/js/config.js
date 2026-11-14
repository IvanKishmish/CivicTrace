export const catalogConfig = {
  mode: 'api',
  apiBaseUrl: location.hostname === '127.0.0.1' && location.port === '4173' ? 'http://localhost:5000' : location.origin,
  staticUrl: new URL('../procurements.seed.json', import.meta.url),
  pageSize: 20,
};
