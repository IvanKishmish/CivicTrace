const isLocalDevServer =
  location.hostname === '127.0.0.1' || location.hostname === 'localhost';

const defaultApiBase =
  location.port === '4173'
    ? 'http://localhost:5000/api/v1'
    : `${location.origin}/api/v1`;

export const CONFIG = {
  apiBaseUrl: window.CIVICTRACE_API_BASE || defaultApiBase,
  seedFallbackUrls: [
    './procurements.seed.json',
    '/data/procurements.seed.json',
    '../../data/procurements.seed.json'
  ],
  pageSize: 10,
  poltavaCenter: [49.5883, 34.5514],
  defaultZoom: 13
};

export function isSameOriginApi() {
  return CONFIG.apiBaseUrl.startsWith(location.origin);
}

export { isLocalDevServer };