const defaultApiBase =
  location.port === '4173'
    ? 'http://localhost:5000/api/v1'
    : `${location.origin}/api/v1`;

export const CONFIG = {
  apiBaseUrl: window.CIVICTRACE_API_BASE || defaultApiBase,
  poltavaCenter: [49.5883, 34.5514],
  defaultZoom: 13
};