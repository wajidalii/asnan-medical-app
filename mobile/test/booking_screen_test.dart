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
import 'package:asnan/features/booking/presentation/booking_screen.dart';

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

Map<String, dynamic> _availabilityWithOneSlot() => {
      'doctorId': 'doctor-1',
      'timeZoneId': 'UTC',
      'date': '2026-09-01',
      'slots': [
        {'startUtc': '2026-09-01T09:00:00Z', 'endUtc': '2026-09-01T09:30:00Z'},
      ],
    };

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
    child: MaterialApp(theme: AppTheme.light, home: const BookingScreen(doctorId: 'doctor-1')),
  );
}

void main() {
  testWidgets('selecting a slot creates a hold and shows the confirmation screen', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path.startsWith('/availability/doctors/')) {
        return _json(_availabilityWithOneSlot());
      }
      return _json({
        'id': 'hold-1',
        'doctorId': 'doctor-1',
        'slotStartUtc': '2026-09-01T09:00:00Z',
        'slotEndUtc': '2026-09-01T09:30:00Z',
        'holdToken': 'raw-token',
        'expiresAtUtc': DateTime.now().toUtc().add(const Duration(minutes: 5)).toIso8601String(),
      }, statusCode: 201);
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    await tester.tap(find.byType(OutlinedButton).first);
    // Not pumpAndSettle from here: a real 5-minute-out hold starts a
    // Timer.periodic(1s) countdown that never "settles" on its own.
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 50));

    expect(find.text('Slot held'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Cancel hold'), findsOneWidget);

    // Cancel explicitly so the countdown timer is torn down before the test
    // ends (an unfulfilled Timer.periodic reads as a leaked-timer test failure).
    await tester.tap(find.widgetWithText(OutlinedButton, 'Cancel hold'));
    await tester.pump();
  });

  testWidgets('hold conflict shows a clear error and stays on slot selection', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path.startsWith('/availability/doctors/')) {
        return _json(_availabilityWithOneSlot());
      }
      throw DioException(
        requestOptions: options,
        type: DioExceptionType.badResponse,
        response: Response(
          requestOptions: options,
          statusCode: 409,
          data: {'title': 'Someone else just booked this slot.'},
        ),
      );
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    await tester.tap(find.byType(OutlinedButton).first);
    await tester.pumpAndSettle();

    expect(find.text('Someone else just booked this slot.'), findsOneWidget);
    expect(find.text('Slot held'), findsNothing);
  });

  testWidgets('hold expiry returns to slot selection with a clear message', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path.startsWith('/availability/doctors/')) {
        return _json(_availabilityWithOneSlot());
      }
      // Already expired by the time it's returned, so the first countdown
      // tick (1 second of pumped virtual time) fires the expiry path.
      return _json({
        'id': 'hold-1',
        'doctorId': 'doctor-1',
        'slotStartUtc': '2026-09-01T09:00:00Z',
        'slotEndUtc': '2026-09-01T09:30:00Z',
        'holdToken': 'raw-token',
        'expiresAtUtc': DateTime.now().toUtc().subtract(const Duration(seconds: 1)).toIso8601String(),
      }, statusCode: 201);
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    await tester.tap(find.byType(OutlinedButton).first);
    // Not pumpAndSettle: the countdown timer is running until the tick below
    // detects the already-past expiresAtUtc and cancels itself.
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 50));
    expect(find.text('Slot held'), findsOneWidget);

    await tester.pump(const Duration(seconds: 1));
    // The expiry path re-fetches slots (a real async call) — safe to settle
    // now since _tick() already cancelled the periodic timer.
    await tester.pumpAndSettle();

    expect(find.text('Slot held'), findsNothing);
    expect(find.text('Your hold has expired. Please pick another slot.'), findsOneWidget);
  });
}
