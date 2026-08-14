import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'package:asnan/core/network/access_token_holder.dart';
import 'package:asnan/core/network/dio_client.dart';
import 'package:asnan/core/router/app_router.dart';
import 'package:asnan/core/storage/secure_storage_service.dart';
import 'package:asnan/core/theme/app_theme.dart';
import 'package:asnan/features/doctors/presentation/doctor_detail_screen.dart';
import 'package:asnan/features/doctors/presentation/doctor_list_screen.dart';
import 'package:asnan/features/booking/presentation/booking_screen.dart';
import 'package:asnan/features/payments/presentation/payment_confirmation_screen.dart';
import 'package:asnan/features/payments/presentation/payment_review_screen.dart';

import 'fakes/fake_secure_storage_service.dart';

/// Critical-flow coverage (issue #37): the full booking -> payment ->
/// confirmation journey, driving real screens through real GoRouter
/// navigation (home -> doctor detail -> booking -> payment review ->
/// payment confirmation) — proves the hand-off between BookingController's
/// held slot and PaymentController's checkout actually survives real
/// pushNamed navigation, which payment_confirmation_screen_test.dart's
/// pre-seeded-state approach doesn't exercise.
///
/// Uses a router scoped to just this flow's routes (same convention as
/// payment_confirmation_screen_test.dart) rather than the full AppRouter,
/// since this flow starts already authenticated and the full router's
/// splash/login gate isn't what's under test here.
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

Map<String, dynamic> _doctorSummaryJson() => {
      'id': 'doctor-1',
      'fullName': 'Dr. Flow Test',
      'bio': null,
      'consultationFee': 120.0,
      'currency': 'USD',
      'yearsOfExperience': 5,
      'clinicAddress': null,
      'isAcceptingNewPatients': true,
      'specialties': [],
    };

Map<String, dynamic> _doctorDetailJson() => {
      'id': 'doctor-1',
      'fullName': 'Dr. Flow Test',
      'bio': null,
      'qualifications': null,
      'specialties': [],
      'yearsOfExperience': 5,
      'consultationFee': 120.0,
      'currency': 'USD',
      'clinicAddress': null,
      'appointmentDurationMinutes': 30,
      'isAcceptingNewPatients': true,
    };

Map<String, dynamic> _availabilityWithOneSlot() => {
      'doctorId': 'doctor-1',
      'timeZoneId': 'UTC',
      'date': '2026-09-01',
      'slots': [
        {'startUtc': '2026-09-01T09:00:00Z', 'endUtc': '2026-09-01T09:30:00Z'},
      ],
    };

Widget _wrap(HttpClientAdapter adapter) {
  final router = GoRouter(
    initialLocation: '/home',
    routes: [
      GoRoute(
        path: '/home',
        name: AppRoutes.home,
        builder: (context, state) => const DoctorListScreen(),
        routes: [
          GoRoute(
            path: 'doctors/:id',
            name: AppRoutes.doctorDetail,
            builder: (context, state) => DoctorDetailScreen(doctorId: state.pathParameters['id']!),
            routes: [
              GoRoute(
                path: 'book',
                name: AppRoutes.booking,
                builder: (context, state) => BookingScreen(doctorId: state.pathParameters['id']!),
                routes: [
                  GoRoute(
                    path: 'review',
                    name: AppRoutes.paymentReview,
                    builder: (context, state) => PaymentReviewScreen(doctorId: state.pathParameters['id']!),
                  ),
                  GoRoute(
                    path: 'confirmation',
                    name: AppRoutes.paymentConfirmation,
                    builder: (context, state) => PaymentConfirmationScreen(doctorId: state.pathParameters['id']!),
                  ),
                ],
              ),
            ],
          ),
          GoRoute(
            path: 'appointments',
            name: AppRoutes.appointments,
            builder: (context, state) => const Scaffold(body: Text('Appointments screen')),
          ),
        ],
      ),
    ],
  );

  return ProviderScope(
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
    child: MaterialApp.router(theme: AppTheme.light, routerConfig: router),
  );
}

void main() {
  testWidgets('home -> doctor detail -> booking -> payment review -> confirmation shows Scheduled', (tester) async {
    var checkoutPollCount = 0;

    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/specialties') return _json([]);
      if (options.path == '/appointments') return _json({'items': [], 'page': 1, 'pageSize': 20, 'totalCount': 0});
      if (options.path == '/doctors') {
        return _json({'items': [_doctorSummaryJson()], 'page': 1, 'pageSize': 20, 'totalCount': 1});
      }
      if (options.path == '/doctors/doctor-1') return _json(_doctorDetailJson());
      if (options.path.startsWith('/availability/doctors/')) return _json(_availabilityWithOneSlot());

      if (options.path == '/appointments/holds') {
        return _json({
          'id': 'hold-1',
          'doctorId': 'doctor-1',
          'slotStartUtc': '2026-09-01T09:00:00Z',
          'slotEndUtc': '2026-09-01T09:30:00Z',
          'holdToken': 'raw-token',
          'expiresAtUtc': DateTime.now().toUtc().add(const Duration(minutes: 5)).toIso8601String(),
        }, statusCode: 201);
      }

      if (options.path == '/payments/checkout') {
        // First call is the real checkout (PaymentPending); every poll call
        // after the mock webhook is delivered observes Scheduled — proving
        // the confirmation screen only shows success once the backend (not
        // the client-reported mock outcome) says so.
        checkoutPollCount++;
        final status = checkoutPollCount == 1 ? 1 : 2; // 1 = PaymentPending, 2 = Scheduled
        return _json({
          'appointmentId': 'appt-1',
          'paymentTransactionId': 'txn-1',
          'providerSessionId': 'mock_sess_1',
          'redirectUrl': '/api/v1/payments/mock/sessions/mock_sess_1/confirm',
          'amount': 120.0,
          'currency': 'USD',
          'status': status,
        });
      }

      if (options.path == '/payments/mock/sessions/mock_sess_1/confirm') {
        return _json({
          'providerEventId': 'evt-1',
          'rawBody': '{}',
          'signatureHeaderValue': 'sig-1',
        });
      }

      if (options.path == '/payments/webhook') return _json({});

      throw DioException(requestOptions: options, type: DioExceptionType.connectionError);
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    // Home -> doctor detail.
    await tester.tap(find.text('Dr. Flow Test'));
    await tester.pumpAndSettle();
    expect(find.text('Doctor Profile'), findsOneWidget);

    // Doctor detail -> booking.
    await tester.tap(find.widgetWithText(FilledButton, 'Book Appointment'));
    await tester.pumpAndSettle();
    expect(find.text('Book Appointment'), findsWidgets);

    // Select the only slot -> hold created.
    await tester.tap(find.byType(OutlinedButton).first);
    // Not pumpAndSettle: the hold's countdown timer never settles on its own.
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 50));
    expect(find.text('Slot held'), findsOneWidget);

    // Booking -> payment review. Not pumpAndSettle from here on: the hold's
    // BookingController instance stays alive underneath (push, not go — see
    // PaymentReviewScreen's own doc comment), so its 1s countdown
    // Timer.periodic keeps ticking in the background and would make
    // pumpAndSettle spin forever.
    await tester.tap(find.widgetWithText(FilledButton, 'Proceed to Payment'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 50));
    expect(find.text('Review & Pay'), findsOneWidget);

    // Start checkout -> payment confirmation (mock-provider handoff).
    await tester.tap(find.widgetWithText(FilledButton, 'Pay USD 120'));
    for (var i = 0; i < 6; i++) {
      await tester.pump(const Duration(milliseconds: 50));
    }
    expect(find.text('Mock Payment Provider'), findsOneWidget);

    // Simulate a successful payment -> webhook relay -> polling -> Scheduled.
    // Three sequential HTTP round-trips happen here (mock confirm, webhook
    // relay, then the first poll) — pump repeatedly to flush all of them
    // rather than guessing a single duration.
    await tester.tap(find.widgetWithText(FilledButton, 'Simulate Successful Payment'));
    for (var i = 0; i < 6; i++) {
      await tester.pump(const Duration(milliseconds: 50));
    }

    expect(find.text('Appointment Confirmed'), findsOneWidget);
  });
}
