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
import 'package:asnan/features/appointments/presentation/appointments_list_screen.dart';

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

Widget _wrap(HttpClientAdapter adapter) {
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
    child: MaterialApp(theme: AppTheme.light, home: const AppointmentsListScreen()),
  );
}

Map<String, dynamic> _pagedResult(List<Map<String, dynamic>> items) =>
    {'items': items, 'page': 1, 'pageSize': 20, 'totalCount': items.length};

void main() {
  testWidgets('shows appointments returned by the API on the Upcoming tab', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      return _json(_pagedResult([
        {
          'id': 'appt-1',
          'doctorProfileId': 'doctor-1',
          'doctorFullName': 'Dr. Jane List',
          'slotStartUtc': '2026-09-01T09:00:00Z',
          'slotEndUtc': '2026-09-01T09:30:00Z',
          'status': 2,
          'consultationFee': 120.0,
          'currency': 'USD',
        },
      ]));
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    expect(find.text('Dr. Jane List'), findsOneWidget);
  });

  testWidgets('shows an empty state when there are no upcoming appointments', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async => _json(_pagedResult([])));

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    expect(find.text('No upcoming appointments. Book one from the doctor directory.'), findsOneWidget);
  });

  testWidgets('shows an error state with a retry button on failure', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      throw DioException(requestOptions: options, type: DioExceptionType.connectionError);
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    expect(find.text('Network error. Please check your connection and try again.'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Retry'), findsOneWidget);
  });

  testWidgets('switching to the Past tab loads that scope independently', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      final scope = options.queryParameters['scope'];
      if (scope == 'Past') {
        return _json(_pagedResult([
          {
            'id': 'appt-2',
            'doctorProfileId': 'doctor-2',
            'doctorFullName': 'Dr. Past Visit',
            'slotStartUtc': '2026-01-01T09:00:00Z',
            'slotEndUtc': '2026-01-01T09:30:00Z',
            'status': 3,
            'consultationFee': 90.0,
            'currency': 'USD',
          },
        ]));
      }
      return _json(_pagedResult([]));
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Past'));
    await tester.pumpAndSettle();

    expect(find.text('Dr. Past Visit'), findsOneWidget);
  });
}
