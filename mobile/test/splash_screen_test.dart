import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/core/network/session_refresher.dart';
import 'package:asnan/core/storage/secure_storage_service.dart';
import 'package:asnan/features/notifications/data/fcm_service.dart';
import 'package:asnan/main.dart';

import 'fakes/fake_secure_storage_service.dart';

/// Issue #38: the splash screen's cold-start silent-refresh has three
/// distinct outcomes, and only "no stored session -> login" (widget_test.dart)
/// was previously covered. This adds the other two: a revoked/expired
/// refresh token (a real rejection — routes to login, same as no session),
/// and a refresh that can't reach the backend at all (offline — must NOT be
/// treated as "not logged in"; needs its own retry affordance).
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

Future<FakeSecureStorageService> _storageWithRefreshToken() async {
  final storage = FakeSecureStorageService();
  await storage.saveRefreshToken('stored-refresh-token');
  return storage;
}

void main() {
  testWidgets('a revoked refresh token routes to login', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      throw DioException(
        requestOptions: options,
        type: DioExceptionType.badResponse,
        response: Response(requestOptions: options, statusCode: 401),
      );
    });

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          secureStorageProvider.overrideWithValue(await _storageWithRefreshToken()),
          fcmServiceProvider.overrideWithValue(null),
          refreshDioProvider.overrideWith((ref) {
            final dio = Dio(BaseOptions(baseUrl: 'http://test.local'));
            dio.httpClientAdapter = adapter;
            return dio;
          }),
        ],
        child: const AsnanApp(),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Sign in'), findsWidgets);
  });

  testWidgets('an unreachable backend shows a retry instead of routing anywhere', (tester) async {
    var attempts = 0;
    final adapter = _StubHttpClientAdapter((options) async {
      attempts++;
      throw DioException(requestOptions: options, type: DioExceptionType.connectionError);
    });

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          secureStorageProvider.overrideWithValue(await _storageWithRefreshToken()),
          fcmServiceProvider.overrideWithValue(null),
          refreshDioProvider.overrideWith((ref) {
            final dio = Dio(BaseOptions(baseUrl: 'http://test.local'));
            dio.httpClientAdapter = adapter;
            return dio;
          }),
        ],
        child: const AsnanApp(),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text("We couldn't verify your session. Check your connection and try again."), findsOneWidget);
    expect(find.text('Sign in'), findsNothing);
    expect(attempts, 1);

    await tester.tap(find.widgetWithText(OutlinedButton, 'Retry'));
    await tester.pumpAndSettle();

    expect(find.text("We couldn't verify your session. Check your connection and try again."), findsOneWidget);
    expect(attempts, 2);
  });
}
