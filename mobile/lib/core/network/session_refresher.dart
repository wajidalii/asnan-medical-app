import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../storage/secure_storage_service.dart';
import 'access_token_holder.dart';
import 'api_config.dart';

/// Thrown when a refresh attempt couldn't reach the backend at all
/// (offline/timeout) — distinct from the backend explicitly rejecting the
/// refresh token, which means the session really is over. Callers should
/// treat this as "unknown, try again," never as "not logged in."
class SessionRestoreUnavailableException implements Exception {}

bool _isConnectivityFailure(DioException e) => switch (e.type) {
      DioExceptionType.connectionError ||
      DioExceptionType.connectionTimeout ||
      DioExceptionType.receiveTimeout ||
      DioExceptionType.sendTimeout =>
        true,
      _ => false,
    };

/// Its own bare Dio instance (no interceptors) rather than the app's shared
/// `dioProvider` client, since routing this call through the intercepted
/// client would recurse back into this same refresh logic on a 401. Exposed
/// as a provider (rather than constructed inline) purely so tests can swap
/// in a stub adapter — this call sits outside `dioProvider`'s override seam
/// by design (see [refreshAccessToken]'s doc comment).
final refreshDioProvider = Provider<Dio>((ref) => Dio(BaseOptions(
      baseUrl: apiBaseUrl,
      // Bounded timeouts (same as the app's shared dioProvider client) so a
      // silently-dropped connection fails fast into the catch block below
      // instead of leaving the caller hanging indefinitely.
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 10),
    )));

/// Exchanges the stored refresh token for a new access/refresh pair against
/// POST /auth/refresh (see ARCHITECTURE.md §4.3/§4.5).
///
/// Shared by two callers that must not depend on each other: the splash
/// screen's cold-start silent refresh, and DioClient's 401-triggered retry
/// interceptor.
Future<String?> refreshAccessToken(Ref ref) async {
  final secureStorage = ref.read(secureStorageProvider);
  final refreshToken = await secureStorage.readRefreshToken();
  if (refreshToken == null) {
    return null;
  }

  final rawDio = ref.read(refreshDioProvider);

  try {
    final response = await rawDio.post<Map<String, dynamic>>(
      '/auth/refresh',
      data: {'refreshToken': refreshToken},
    );

    final data = response.data!;
    final newAccessToken = data['accessToken'] as String;
    final newRefreshToken = data['refreshToken'] as String;

    await secureStorage.saveRefreshToken(newRefreshToken);
    ref.read(accessTokenHolderProvider.notifier).set(newAccessToken);

    return newAccessToken;
  } on DioException catch (e) {
    if (_isConnectivityFailure(e)) {
      throw SessionRestoreUnavailableException();
    }

    // Invalid, expired, or revoked (including reuse-detected) — the session
    // is over either way. Clear local state so the caller routes to login.
    await secureStorage.deleteRefreshToken();
    ref.read(accessTokenHolderProvider.notifier).set(null);
    return null;
  }
}
