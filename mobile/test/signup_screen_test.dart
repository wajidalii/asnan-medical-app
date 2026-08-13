import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/core/network/access_token_holder.dart';
import 'package:asnan/core/network/dio_client.dart';
import 'package:asnan/core/storage/secure_storage_service.dart';
import 'package:asnan/core/theme/app_theme.dart';
import 'package:asnan/features/auth/presentation/signup_screen.dart';

import 'fakes/fake_secure_storage_service.dart';

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

void main() {
  testWidgets('empty destination is rejected without calling the API', (tester) async {
    var called = false;
    final adapter = _StubHttpClientAdapter((options) async {
      called = true;
      throw DioException(requestOptions: options, type: DioExceptionType.connectionError);
    });

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          secureStorageProvider.overrideWithValue(FakeSecureStorageService()),
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
        child: MaterialApp(theme: AppTheme.light, home: const SignupScreen()),
      ),
    );

    await tester.tap(find.widgetWithText(FilledButton, 'Send code'));
    await tester.pump();

    expect(find.text('Required'), findsOneWidget);
    expect(called, isFalse);
  });

  testWidgets('switching to mobile channel updates the field label', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [secureStorageProvider.overrideWithValue(FakeSecureStorageService())],
        child: MaterialApp(theme: AppTheme.light, home: const SignupScreen()),
      ),
    );

    expect(find.text('Email address'), findsOneWidget);

    await tester.tap(find.text('Mobile'));
    await tester.pump();

    expect(find.text('Mobile number'), findsOneWidget);
  });
}
