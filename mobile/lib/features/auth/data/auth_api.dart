import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/dio_client.dart';
import '../domain/otp_channel.dart';
import 'login_response.dart';

/// Thin wrapper over the raw HTTP calls — no business logic, no local state.
/// AuthRepository is the layer that decides what to do with the results.
class AuthApi {
  AuthApi(this._dio);

  final Dio _dio;

  Future<void> requestSignupOtp(String destination, OtpChannel channel) => _dio.post(
        '/auth/signup/request-otp',
        data: {'destination': destination, 'channel': channel.value},
      );

  Future<String> verifySignupOtp(String destination, String code, OtpChannel channel) async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/auth/signup/verify-otp',
      data: {'destination': destination, 'code': code, 'channel': channel.value},
    );
    return response.data!['signupToken'] as String;
  }

  Future<void> setPassword(String signupToken, String password) => _dio.post(
        '/auth/signup/set-password',
        data: {'signupToken': signupToken, 'password': password},
      );

  Future<LoginResponse> login({
    required String identifier,
    required String password,
    required String deviceId,
    String? deviceName,
  }) async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/auth/login',
      data: {
        'identifier': identifier,
        'password': password,
        'deviceId': deviceId,
        'deviceName': deviceName,
      },
    );
    return LoginResponse.fromJson(response.data!);
  }

  Future<void> logout(String refreshToken) => _dio.post(
        '/auth/logout',
        data: {'refreshToken': refreshToken},
      );

  Future<void> logoutAll() => _dio.post('/auth/logout-all');
}

/// Uses the shared intercepted `dioProvider` client — signup/login calls
/// don't need an access token (none exists yet at that point), but logout
/// and logout-all do, and the interceptor handles that uniformly either way.
final authApiProvider = Provider<AuthApi>((ref) => AuthApi(ref.watch(dioProvider)));
