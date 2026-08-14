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
import 'package:asnan/features/booking/domain/hold.dart';
import 'package:asnan/features/booking/presentation/booking_controller.dart';
import 'package:asnan/features/booking/presentation/booking_state.dart';
import 'package:asnan/features/doctors/domain/doctor_detail.dart';
import 'package:asnan/features/doctors/presentation/doctor_detail_controller.dart';
import 'package:asnan/features/payments/presentation/payment_confirmation_screen.dart';
import 'package:asnan/features/payments/presentation/payment_review_screen.dart';

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

Hold _activeHold() => Hold(
      id: 'hold-1',
      doctorId: 'doctor-1',
      slotStartUtc: DateTime.utc(2026, 9, 1, 9),
      slotEndUtc: DateTime.utc(2026, 9, 1, 9, 30),
      holdToken: 'raw-hold-token',
      expiresAtUtc: DateTime.now().toUtc().add(const Duration(minutes: 5)),
    );

DoctorDetail _doctor() => const DoctorDetail(
      id: 'doctor-1',
      fullName: 'Dr. Jane Payments',
      bio: null,
      qualifications: null,
      specialties: [],
      yearsOfExperience: 5,
      consultationFee: 120,
      currency: 'USD',
      clinicAddress: '123 Health St',
      appointmentDurationMinutes: 30,
      isAcceptingNewPatients: true,
    );

Widget _wrap(HttpClientAdapter adapter, {required BookingState bookingState}) {
  final router = GoRouter(
    initialLocation: '/review/doctor-1',
    routes: [
      GoRoute(
        path: '/review/:id',
        name: AppRoutes.paymentReview,
        builder: (context, state) => PaymentReviewScreen(doctorId: state.pathParameters['id']!),
      ),
      GoRoute(
        path: '/confirmation/:id',
        name: AppRoutes.paymentConfirmation,
        builder: (context, state) => PaymentConfirmationScreen(doctorId: state.pathParameters['id']!),
      ),
    ],
  );

  final container = ProviderContainer(
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
      doctorDetailProvider('doctor-1').overrideWith((ref) async => _doctor()),
    ],
  );
  // Seed the shared BookingController state (same provider PaymentReviewScreen
  // reads) directly — realistic setup would drive a real hold through
  // BookingScreen first, but the payments feature only needs an already-held
  // slot to exist, same as if the user arrived here from BookingScreen.
  container.read(bookingControllerProvider('doctor-1').notifier).state = bookingState;

  return UncontrolledProviderScope(
    container: container,
    child: MaterialApp.router(theme: AppTheme.light, routerConfig: router),
  );
}

void main() {
  testWidgets('shows doctor, slot time, and fee before payment', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async => _json({}));

    await tester.pumpWidget(_wrap(adapter, bookingState: BookingState(doctorId: 'doctor-1', activeHold: _activeHold())));
    await tester.pumpAndSettle();

    expect(find.text('Dr. Jane Payments'), findsOneWidget);
    expect(find.text('123 Health St'), findsOneWidget);
    expect(find.text('Pay USD 120'), findsOneWidget);
  });

  testWidgets('paying checks out and navigates to the confirmation screen', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/payments/checkout') {
        return _json({
          'appointmentId': 'appt-1',
          'paymentTransactionId': 'txn-1',
          'providerSessionId': 'mock_sess_1',
          'redirectUrl': '/api/v1/payments/mock/sessions/mock_sess_1/confirm',
          'amount': 120.0,
          'currency': 'USD',
          'status': 1,
        });
      }
      return _json({});
    });

    await tester.pumpWidget(_wrap(adapter, bookingState: BookingState(doctorId: 'doctor-1', activeHold: _activeHold())));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Pay USD 120'));
    await tester.pumpAndSettle();

    expect(find.text('Mock Payment Provider'), findsOneWidget);
  });

  testWidgets('checkout failure shows an error banner and stays on the review screen', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/payments/checkout') {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 409, data: {'title': 'This hold has expired.'}),
        );
      }
      return _json({});
    });

    await tester.pumpWidget(_wrap(adapter, bookingState: BookingState(doctorId: 'doctor-1', activeHold: _activeHold())));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Pay USD 120'));
    await tester.pumpAndSettle();

    expect(find.text('This hold has expired.'), findsOneWidget);
    expect(find.text('Mock Payment Provider'), findsNothing);
  });

  testWidgets('with no active hold, shows a message instead of the review card', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async => _json({}));

    await tester.pumpWidget(_wrap(adapter, bookingState: const BookingState(doctorId: 'doctor-1')));
    await tester.pumpAndSettle();

    expect(find.text('Your hold is no longer active. Please select a slot again.'), findsOneWidget);
  });
}
