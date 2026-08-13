import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:uuid/uuid.dart';

import '../../../core/network/access_token_holder.dart';
import '../../../core/storage/secure_storage_service.dart';
import '../domain/auth_failure.dart';
import '../domain/auth_result.dart';
import '../domain/otp_channel.dart';
import 'auth_api.dart';

class AuthRepository {
  AuthRepository(this._api, this._secureStorage, this._accessTokenHolder);

  final AuthApi _api;
  final SecureStorageService _secureStorage;
  final AccessTokenHolder _accessTokenHolder;

  Future<AuthResult<void>> requestSignupOtp(String destination, OtpChannel channel) =>
      _guard(() => _api.requestSignupOtp(destination, channel));

  Future<AuthResult<String>> verifySignupOtp(String destination, String code, OtpChannel channel) =>
      _guard(() => _api.verifySignupOtp(destination, code, channel));

  Future<AuthResult<void>> setPassword(String signupToken, String password) =>
      _guard(() => _api.setPassword(signupToken, password));

  Future<AuthResult<void>> login(String identifier, String password, {String? deviceName}) =>
      _guard(() async {
        final deviceId = await _deviceId();
        final response = await _api.login(
          identifier: identifier,
          password: password,
          deviceId: deviceId,
          deviceName: deviceName,
        );
        await _secureStorage.saveRefreshToken(response.refreshToken);
        _accessTokenHolder.set(response.accessToken);
      });

  /// Called once on cold start (see SplashScreen). Not routed through
  /// AuthApi's Dio client — see refreshAccessToken's own doc comment on why.
  Future<bool> tryRestoreSession(Future<String?> Function() refresh) async {
    final token = await refresh();
    return token != null;
  }

  Future<void> logout() async {
    final refreshToken = await _secureStorage.readRefreshToken();
    if (refreshToken != null) {
      try {
        await _api.logout(refreshToken);
      } on DioException {
        // Best-effort: the token still gets cleared locally below either way.
      }
    }
    await _secureStorage.deleteRefreshToken();
    _accessTokenHolder.set(null);
  }

  Future<void> logoutAllDevices() async {
    try {
      await _api.logoutAll();
    } on DioException {
      // Same best-effort stance as logout().
    }
    await _secureStorage.deleteRefreshToken();
    _accessTokenHolder.set(null);
  }

  Future<String> _deviceId() async {
    final existing = await _secureStorage.readDeviceId();
    if (existing != null) return existing;

    final generated = const Uuid().v4();
    await _secureStorage.saveDeviceId(generated);
    return generated;
  }

  Future<AuthResult<T>> _guard<T>(Future<T> Function() action) async {
    try {
      return AuthSuccess(await action());
    } on DioException catch (e) {
      return AuthError(AuthFailure.fromDioException(e));
    }
  }
}

final authRepositoryProvider = Provider<AuthRepository>((ref) => AuthRepository(
      ref.watch(authApiProvider),
      ref.watch(secureStorageProvider),
      ref.watch(accessTokenHolderProvider.notifier),
    ));
