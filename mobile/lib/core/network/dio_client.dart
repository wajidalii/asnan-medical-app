import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'access_token_holder.dart';
import 'api_config.dart';
import 'session_refresher.dart';

/// Called on a 401 to obtain a fresh access token. Returns null if refresh
/// failed (session is over — caller should route to login).
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

final dioProvider = Provider<Dio>((ref) {
  final dio = Dio(
    BaseOptions(
      baseUrl: apiBaseUrl,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 10),
    ),
  );

  dio.interceptors.add(
    AuthInterceptor(
      dio: dio,
      readAccessToken: () => ref.read(accessTokenHolderProvider),
      onRefresh: () => refreshAccessToken(ref),
    ),
  );

  return dio;
});
