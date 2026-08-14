import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/auth/presentation/auth_controller.dart';
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
    this.onSessionExpired,
  });

  final String? Function() readAccessToken;
  final RefreshTokenCallback onRefresh;
  final Dio dio;

  /// Called once when a mid-use refresh fails — i.e. the session is over,
  /// not just this one request. Optional so every existing test construction
  /// of this interceptor keeps compiling unchanged.
  final void Function()? onSessionExpired;

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

    String? newToken;
    try {
      newToken = await _inFlightRefresh;
    } on SessionRestoreUnavailableException {
      // Couldn't reach the backend to refresh — this request's own offline
      // error is the right thing to surface, not "your session is over."
      handler.next(err);
      return;
    }

    if (newToken == null) {
      onSessionExpired?.call();
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
      onSessionExpired: () => ref.read(authControllerProvider.notifier).sessionExpired(),
    ),
  );

  return dio;
});
