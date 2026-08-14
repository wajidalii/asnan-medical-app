import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/core/network/access_token_holder.dart';
import 'package:asnan/core/network/dio_client.dart';
import 'package:asnan/core/storage/secure_storage_service.dart';
import 'package:asnan/features/auth/presentation/auth_controller.dart';
import 'package:asnan/features/notifications/data/fcm_service.dart';
import 'package:asnan/main.dart';

import 'fakes/fake_secure_storage_service.dart';

/// Issue #38's specifically-called-out requirement: a session that expires
/// or is revoked mid-use (a 401 whose refresh attempt also fails) must
/// route the user to login with a clear message, not fail silently on
/// whatever screen they were on. This drives the real `AsnanApp` and its
/// real `AuthInterceptor`/`AuthController` wiring end to end — the same
/// mechanism a revoked refresh token hits in production — rather than
/// asserting against AuthController state directly.
class _StubHttpClientAdapter implements HttpClientAdapter {
  _StubHttpClientAdapter(this._handler);

  final Future<ResponseBody> Function(RequestOptions options) _handler;

  @override
  void close({bool force = false}) {}

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) =>
      _handler(options);
}

ResponseBody _json(Object json, {int statusCode = 200}) => ResponseBody.fromString(
      jsonEncode(json),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

void main() {
  testWidgets('a 401 whose refresh also fails routes to login with a session-expired message', (tester) async {
    var sessionRevoked = false;

    final adapter = _StubHttpClientAdapter((options) async {
      switch (options.path) {
        case '/auth/login':
          return _json({
            'accessToken': 'access-1',
            'accessTokenExpiresAtUtc': DateTime.now().toUtc().add(const Duration(minutes: 15)).toIso8601String(),
            'refreshToken': 'refresh-1',
            'refreshTokenExpiresAtUtc': DateTime.now().toUtc().add(const Duration(days: 30)).toIso8601String(),
          });
        case '/specialties':
          return _json([]);
        case '/doctors':
          return _json({'items': [], 'page': 1, 'pageSize': 20, 'totalCount': 0});
        case '/appointments':
          if (sessionRevoked) {
            throw DioException(
              requestOptions: options,
              type: DioExceptionType.badResponse,
              response: Response(requestOptions: options, statusCode: 401),
            );
          }
          return _json({'items': [], 'page': 1, 'pageSize': 20, 'totalCount': 0});
        default:
          throw DioException(requestOptions: options, type: DioExceptionType.connectionError);
      }
    });

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          secureStorageProvider.overrideWithValue(FakeSecureStorageService()),
          fcmServiceProvider.overrideWithValue(null),
          dioProvider.overrideWith((ref) {
            final dio = Dio(BaseOptions(baseUrl: 'http://test.local'));
            dio.httpClientAdapter = adapter;
            dio.interceptors.add(AuthInterceptor(
              dio: dio,
              readAccessToken: () => ref.read(accessTokenHolderProvider),
              // Simulates a revoked/expired refresh token — the same
              // outcome refreshAccessToken() produces when the backend
              // rejects the stored refresh token.
              onRefresh: () async => null,
              onSessionExpired: () => ref.read(authControllerProvider.notifier).sessionExpired(),
            ));
            return dio;
          }),
        ],
        child: const AsnanApp(),
      ),
    );

    // Splash -> login (no stored session) -> log in -> home.
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextFormField, 'Email or mobile number'), 'patient@test.local');
    await tester.enterText(find.widgetWithText(TextFormField, 'Password'), 'correct horse battery staple');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();
    expect(find.text('Find a Doctor'), findsOneWidget);

    // Mid-use: the next call to a protected endpoint gets a 401, and the
    // simulated refresh fails — the session is over.
    sessionRevoked = true;
    await tester.tap(find.byTooltip('My appointments'));
    await tester.pumpAndSettle();

    expect(find.text('Sign in'), findsWidgets);
    expect(find.text('Your session has expired. Please sign in again.'), findsOneWidget);
  });
}
