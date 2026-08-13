/// Environment-specific; overridden via --dart-define (API_BASE_URL) at
/// build time, never hardcoded to a production value.
const _defaultBaseUrl = 'http://localhost:5199/api/v1';

const String apiBaseUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: _defaultBaseUrl,
);
