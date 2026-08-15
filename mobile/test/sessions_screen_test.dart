import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/core/network/access_token_holder.dart';
import 'package:asnan/core/network/dio_client.dart';
import 'package:asnan/core/storage/secure_storage_service.dart';
import 'package:asnan/core/theme/app_theme.dart';
import 'package:asnan/features/auth/presentation/auth_controller.dart';
import 'package:asnan/features/sessions/presentation/sessions_screen.dart';

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

ResponseBody _json(Object json, {int statusCode = 200}) => ResponseBody.fromString(
      jsonEncode(json),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

List<Map<String, dynamic>> _sessionsJson() => [
      {
        'id': 'session-1',
        'deviceId': 'this-device',
        'deviceName': 'iPhone 15',
        'lastSeenAtUtc': '2026-08-14T09:00:00Z',
        'absoluteExpiresAtUtc': '2026-11-14T09:00:00Z',
      },
      {
        'id': 'session-2',
        'deviceId': 'other-device',
        'deviceName': 'Pixel 9',
        'lastSeenAtUtc': '2026-08-10T09:00:00Z',
        'absoluteExpiresAtUtc': '2026-11-10T09:00:00Z',
      },
    ];

Widget _wrap(HttpClientAdapter adapter, {FakeSecureStorageService? secureStorage}) {
  return ProviderScope(
    overrides: [
      secureStorageProvider.overrideWithValue(secureStorage ?? FakeSecureStorageService()),
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
    child: MaterialApp(theme: AppTheme.light, home: const SessionsScreen()),
  );
}

void main() {
  testWidgets('lists sessions with device name and last-seen, marking the current device', (tester) async {
    final storage = FakeSecureStorageService();
    await storage.saveDeviceId('this-device');
    final adapter = _StubHttpClientAdapter((options) async {
      expect(options.path, '/auth/sessions');
      return _json(_sessionsJson());
    });

    await tester.pumpWidget(_wrap(adapter, secureStorage: storage));
    await tester.pumpAndSettle();

    expect(find.text('iPhone 15'), findsOneWidget);
    expect(find.text('Pixel 9'), findsOneWidget);
    expect(find.textContaining('This device'), findsOneWidget);
    expect(find.textContaining('Last seen'), findsNWidgets(2));
  });

  testWidgets('shows an error state with a retry button on load failure', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      throw DioException(
        requestOptions: options,
        type: DioExceptionType.badResponse,
        response: Response(requestOptions: options, statusCode: 500, data: {'title': 'Something went wrong.'}),
      );
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    expect(find.text('Something went wrong.'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Retry'), findsOneWidget);
  });

  testWidgets('logging out another device calls the revoke endpoint and removes its row', (tester) async {
    final storage = FakeSecureStorageService();
    await storage.saveDeviceId('this-device');
    var revokedPath = '';
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/auth/sessions') return _json(_sessionsJson());
      if (options.method == 'DELETE') {
        revokedPath = options.path;
        return ResponseBody.fromString('', 204);
      }
      return _json({});
    });

    await tester.pumpWidget(_wrap(adapter, secureStorage: storage));
    await tester.pumpAndSettle();

    // "Pixel 9" is the other device's row — tap its "Log out" action.
    final pixelRow = find.ancestor(of: find.text('Pixel 9'), matching: find.byType(ListTile));
    await tester.tap(find.descendant(of: pixelRow, matching: find.widgetWithText(TextButton, 'Log out')));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(TextButton, 'Log out').last); // the confirm dialog's action
    await tester.pumpAndSettle();

    expect(revokedPath, '/auth/sessions/session-2');
    expect(find.text('Pixel 9'), findsNothing);
    expect(find.text('iPhone 15'), findsOneWidget); // untouched
  });

  testWidgets('logging out the current device routes back to login', (tester) async {
    final storage = FakeSecureStorageService();
    await storage.saveDeviceId('this-device');
    await storage.saveRefreshToken('a-refresh-token');
    var logoutCalled = false;
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/auth/sessions') return _json(_sessionsJson());
      if (options.path == '/auth/logout') {
        logoutCalled = true;
        return ResponseBody.fromString('', 204);
      }
      return _json({});
    });

    final container = ProviderContainer(overrides: [
      secureStorageProvider.overrideWithValue(storage),
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
    ]);
    addTearDown(container.dispose);
    container.read(authControllerProvider.notifier).markAuthenticated();

    await tester.pumpWidget(UncontrolledProviderScope(
      container: container,
      child: MaterialApp(theme: AppTheme.light, home: const SessionsScreen()),
    ));
    await tester.pumpAndSettle();

    final iphoneRow = find.ancestor(of: find.text('iPhone 15'), matching: find.byType(ListTile));
    await tester.tap(find.descendant(of: iphoneRow, matching: find.widgetWithText(TextButton, 'Log out')));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(TextButton, 'Log out').last);
    await tester.pumpAndSettle();

    expect(logoutCalled, isTrue);
    expect(container.read(authControllerProvider), AuthStatus.unauthenticated);
  });

  testWidgets('logging out all devices calls logout-all', (tester) async {
    final storage = FakeSecureStorageService();
    await storage.saveDeviceId('this-device');
    var logoutAllCalled = false;
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/auth/sessions') return _json(_sessionsJson());
      if (options.path == '/auth/logout-all') {
        logoutAllCalled = true;
        return ResponseBody.fromString('', 204);
      }
      return _json({});
    });

    final container = ProviderContainer(overrides: [
      secureStorageProvider.overrideWithValue(storage),
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
    ]);
    addTearDown(container.dispose);
    container.read(authControllerProvider.notifier).markAuthenticated();

    await tester.pumpWidget(UncontrolledProviderScope(
      container: container,
      child: MaterialApp(theme: AppTheme.light, home: const SessionsScreen()),
    ));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(TextButton, 'Log out all'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(TextButton, 'Log out').last);
    await tester.pumpAndSettle();

    expect(logoutAllCalled, isTrue);
    expect(container.read(authControllerProvider), AuthStatus.unauthenticated);
  });
}
