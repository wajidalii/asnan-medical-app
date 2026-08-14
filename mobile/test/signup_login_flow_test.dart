import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/core/network/access_token_holder.dart';
import 'package:asnan/core/network/dio_client.dart';
import 'package:asnan/core/storage/secure_storage_service.dart';
import 'package:asnan/features/notifications/data/fcm_service.dart';
import 'package:asnan/main.dart';

import 'fakes/fake_secure_storage_service.dart';

/// Critical-flow coverage (issue #37): signup all the way through to a
/// logged-in home screen, driving the real app widget (`AsnanApp`) and its
/// real GoRouter — not a single screen in isolation. Every screen transition
/// in this test is a real `context.go/pushNamed` call succeeding against the
/// real route table, which single-screen tests (signup_screen_test.dart etc.)
/// can't prove on their own.
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
  testWidgets('signup (request OTP -> verify -> set password) lands on login, then login reaches home', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      switch (options.path) {
        case '/auth/signup/request-otp':
          return _json({});
        case '/auth/signup/verify-otp':
          return _json({'signupToken': 'signup-token-1'});
        case '/auth/signup/set-password':
          return _json({});
        case '/auth/login':
          return _json({
            'accessToken': 'access-1',
            'accessTokenExpiresAtUtc': DateTime.now().toUtc().add(const Duration(minutes: 15)).toIso8601String(),
            'refreshToken': 'refresh-1',
            'refreshTokenExpiresAtUtc': DateTime.now().toUtc().add(const Duration(days: 30)).toIso8601String(),
          });
        case '/specialties':
          return _json([]);
        case '/appointments':
          return _json({'items': [], 'page': 1, 'pageSize': 20, 'totalCount': 0});
        case '/doctors':
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
              onRefresh: () async => null,
            ));
            return dio;
          }),
        ],
        child: const AsnanApp(),
      ),
    );

    // Splash -> login (no stored session).
    await tester.pumpAndSettle();
    expect(find.text('Sign in'), findsWidgets);

    // Login -> signup.
    await tester.tap(find.text("Don't have an account? Sign up"));
    await tester.pumpAndSettle();
    expect(find.text('Create account'), findsOneWidget);

    // Signup: request OTP -> signup-otp screen.
    await tester.enterText(find.byType(TextFormField).first, 'new-patient@test.local');
    await tester.tap(find.widgetWithText(FilledButton, 'Send code'));
    await tester.pumpAndSettle();
    expect(find.text('Enter verification code'), findsOneWidget);

    // Verify OTP -> signup-password screen.
    await tester.enterText(find.byType(TextFormField).first, '123456');
    await tester.tap(find.widgetWithText(FilledButton, 'Verify'));
    await tester.pumpAndSettle();
    expect(find.text('Create a password'), findsOneWidget);

    // Set password -> back to login with a confirmation snackbar.
    await tester.enterText(find.byType(TextFormField).first, 'correct horse battery staple');
    await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
    await tester.pumpAndSettle();
    expect(find.text('Sign in'), findsWidgets);
    expect(find.text('Account created. Please sign in.'), findsOneWidget);

    // Login with the new account -> home.
    await tester.enterText(find.widgetWithText(TextFormField, 'Email or mobile number'), 'new-patient@test.local');
    await tester.enterText(find.widgetWithText(TextFormField, 'Password'), 'correct horse battery staple');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();

    expect(find.text('Find a Doctor'), findsOneWidget);
  });
}
