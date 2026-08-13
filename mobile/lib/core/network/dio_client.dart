import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'access_token_holder.dart';

/// Called on a 401 to obtain a fresh access token. Returns null if refresh
/// failed (session is over — caller should route to login).
///
/// The real implementation (rotating-refresh-token exchange against
/// POST /api/v1/auth/refresh) lands with the authentication feature;
/// this scaffold only wires the retry plumbing so that feature can slot
/// straight in without touching the client setup.
typedef RefreshTokenCallback = Future<String?> Function();

/// Single-flighted: concurrent 401s trigger exactly one refresh call,
/// and every request waiting on it retries once the new token is available.
class AuthInterceptor extends Interceptor {
  AuthInterceptor({
    required this.readAccessToken,
    required this.onRefresh,
    required this.dio,
  });

  final String? Function() readAccessToken;
  final RefreshTokenCallback onRefresh;
  final Dio dio;

  Future<String?>? _inFlightRefresh;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    final token = readAccessToken();
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) async {
    final isUnauthorized = err.response?.statusCode == 401;
    if (!isUnauthorized) {
      handler.next(err);
      return;
    }

    _inFlightRefresh ??= onRefresh().whenComplete(() {
      _inFlightRefresh = null;
    });

    final newToken = await _inFlightRefresh;
    if (newToken == null) {
      handler.next(err);
      return;
    }

    try {
      final retryOptions = err.requestOptions;
      retryOptions.headers['Authorization'] = 'Bearer $newToken';
      final response = await dio.fetch(retryOptions);
      handler.resolve(response);
    } on DioException catch (retryError) {
      handler.next(retryError);
    }
  }
}

/// Base URL is environment-specific; overridden via --dart-define
/// (API_BASE_URL) at build time, never hardcoded to a production value.
const _defaultBaseUrl = 'http://localhost:5199/api/v1';

final dioProvider = Provider<Dio>((ref) {
  final dio = Dio(
    BaseOptions(
      baseUrl: const String.fromEnvironment(
        'API_BASE_URL',
        defaultValue: _defaultBaseUrl,
      ),
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 10),
    ),
  );

  dio.interceptors.add(
    AuthInterceptor(
      dio: dio,
      readAccessToken: () => ref.read(accessTokenHolderProvider),
      onRefresh: () async {
        // TODO(auth-feature): exchange the stored refresh token for a new
        // access/refresh pair via POST /api/v1/auth/refresh. Returns null
        // (no refresh performed) until that feature lands.
        return null;
      },
    ),
  );

  return dio;
});
