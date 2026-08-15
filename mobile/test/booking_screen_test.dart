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

    expect(find.text('SLOT RESERVED'), findsOneWidget);
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
    expect(find.text('SLOT RESERVED'), findsNothing);
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
    expect(find.text('SLOT RESERVED'), findsOneWidget);

    await tester.pump(const Duration(seconds: 1));
    // The expiry path re-fetches slots (a real async call) — safe to settle
    // now since _tick() already cancelled the periodic timer.
    await tester.pumpAndSettle();

    expect(find.text('SLOT RESERVED'), findsNothing);
    expect(find.text('Your hold has expired. Please pick another slot.'), findsOneWidget);
  });

  testWidgets('the date strip shows a loading spinner while it loads', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      await Future<void>.delayed(const Duration(milliseconds: 20));
      return _json(_availabilityWithOneSlot());
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pump();

    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    await tester.pumpAndSettle();
  });

  testWidgets('a date-strip load failure (e.g. offline) shows an error with retry, not a silently-empty strip', (tester) async {
    var shouldFail = true;
    final adapter = _StubHttpClientAdapter((options) async {
      if (shouldFail) {
        throw DioException(requestOptions: options, type: DioExceptionType.connectionError);
      }
      return _json(_availabilityWithOneSlot());
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    expect(find.text('Network error. Please check your connection and try again.'), findsOneWidget);
    expect(find.byType(OutlinedButton), findsNothing);

    shouldFail = false;
    await tester.tap(find.widgetWithText(TextButton, 'Retry'));
    await tester.pumpAndSettle();

    expect(find.text('Network error. Please check your connection and try again.'), findsNothing);
    expect(find.byType(OutlinedButton), findsWidgets);
  });

  testWidgets('no available slots for the selected date shows an empty message', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      return _json({'doctorId': 'doctor-1', 'timeZoneId': 'UTC', 'date': '2026-09-01', 'slots': []});
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    expect(find.text('No slots available this day'), findsOneWidget);
  });

  testWidgets('selecting a different date that fails to load shows an error banner with a retry that reloads it', (tester) async {
    final today = DateTime.now();
    String formatDate(DateTime d) =>
        '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';
    final day1 = formatDate(DateTime(today.year, today.month, today.day + 1));

    var day1RequestCount = 0;
    var day1ShouldFail = true;
    final adapter = _StubHttpClientAdapter((options) async {
      final date = options.uri.queryParameters['date'];
      if (date == day1) {
        day1RequestCount++;
        // The first request for day1 is the date strip's own availability
        // probe (must succeed so its chip is tappable); only the explicit
        // selectDate() triggered by tapping that chip fails, until retried.
        if (day1RequestCount > 1 && day1ShouldFail) {
          throw DioException(
            requestOptions: options,
            type: DioExceptionType.badResponse,
            response: Response(requestOptions: options, statusCode: 500, data: {'title': 'Something went wrong. Please try again.'}),
          );
        }
      }
      return _json(_availabilityWithOneSlot());
    });

    final day1Date = DateTime(today.year, today.month, today.day + 1);

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();
    expect(find.byKey(dateCellKey(day1Date)), findsOneWidget);

    await tester.tap(find.byKey(dateCellKey(day1Date)));
    await tester.pumpAndSettle();

    expect(find.text('Something went wrong. Please try again.'), findsOneWidget);
    expect(find.widgetWithText(TextButton, 'Retry'), findsOneWidget);

    day1ShouldFail = false;
    await tester.tap(find.widgetWithText(TextButton, 'Retry'));
    await tester.pumpAndSettle();

    expect(find.text('Something went wrong. Please try again.'), findsNothing);
    expect(find.byType(OutlinedButton), findsWidgets);
  });
}
