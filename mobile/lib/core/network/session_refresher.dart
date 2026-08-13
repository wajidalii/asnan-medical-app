import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../storage/secure_storage_service.dart';
import 'access_token_holder.dart';
import 'api_config.dart';

/// Exchanges the stored refresh token for a new access/refresh pair against
/// POST /auth/refresh (see ARCHITECTURE.md §4.3/§4.5).
///
/// Shared by two callers that must not depend on each other: the splash
/// screen's cold-start silent refresh, and DioClient's 401-triggered retry
/// interceptor. Uses its own bare Dio instance (no interceptors) rather than
/// the app's shared `dioProvider` client, since routing this call through
/// the intercepted client would recurse back into this same refresh logic
/// on a 401.
Future<String?> refreshAccessToken(Ref ref) async {
  final secureStorage = ref.read(secureStorageProvider);
  final refreshToken = await secureStorage.readRefreshToken();
  if (refreshToken == null) {
    return null;
  }

  final rawDio = Dio(BaseOptions(baseUrl: apiBaseUrl));

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
  } on DioException {
    // Invalid, expired, or revoked (including reuse-detected) — the session
    // is over either way. Clear local state so the caller routes to login.
    await secureStorage.deleteRefreshToken();
    ref.read(accessTokenHolderProvider.notifier).set(null);
    return null;
  }
}
