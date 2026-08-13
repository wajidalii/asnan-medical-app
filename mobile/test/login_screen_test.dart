import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/core/network/access_token_holder.dart';
import 'package:asnan/core/network/dio_client.dart';
import 'package:asnan/core/storage/secure_storage_service.dart';
import 'package:asnan/core/theme/app_theme.dart';
import 'package:asnan/features/auth/presentation/login_screen.dart';

import 'fakes/fake_secure_storage_service.dart';

/// A Dio adapter that returns a fixed response/error without any real
/// network call — lets the widget tests exercise loading/error/success
/// states deterministically.
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

Widget _wrap(Widget child, {required HttpClientAdapter adapter}) {
  return ProviderScope(
    overrides: [
      secureStorageProvider.overrideWithValue(FakeSecureStorageService()),
      dioProvider.overrideWith((ref) {
        final dio = Dio(BaseOptions(baseUrl: 'http://test.local'));
        dio.httpClientAdapter = adapter;
        dio.interceptors.add(
          AuthInterceptor(
            dio: dio,
            readAccessToken: () => ref.read(accessTokenHolderProvider),
            onRefresh: () async => null,
          ),
        );
        return dio;
      }),
    ],
    child: MaterialApp(
      theme: AppTheme.light,
      home: child,
    ),
  );
}

void main() {
  testWidgets('shows a generic error message on invalid credentials', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      throw DioException(
        requestOptions: options,
        response: Response(
          requestOptions: options,
          statusCode: 401,
          data: {'title': 'Invalid credentials.'},
        ),
        type: DioExceptionType.badResponse,
      );
    });

    await tester.pumpWidget(_wrap(const LoginScreen(), adapter: adapter));

    await tester.enterText(find.byType(TextFormField).first, 'user@test.local');
    await tester.enterText(find.byType(TextFormField).last, 'wrong-password');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pump();

    // Loading state: the submit button shows a spinner instead of its label.
    expect(find.widgetWithText(FilledButton, 'Sign in'), findsNothing);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    await tester.pumpAndSettle();

    expect(find.text('Invalid credentials.'), findsOneWidget);
  });

  testWidgets('empty form fields are rejected without calling the API', (tester) async {
    var called = false;
    final adapter = _StubHttpClientAdapter((options) async {
      called = true;
      throw DioException(requestOptions: options, type: DioExceptionType.connectionError);
    });

    await tester.pumpWidget(_wrap(const LoginScreen(), adapter: adapter));

    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pump();

    expect(find.text('Required'), findsWidgets);
    expect(called, isFalse);
  });
}
