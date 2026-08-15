import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'package:asnan/core/network/access_token_holder.dart';
import 'package:asnan/core/network/dio_client.dart';
import 'package:asnan/core/router/app_router.dart';
import 'package:asnan/core/storage/secure_storage_service.dart';
import 'package:asnan/core/theme/app_theme.dart';
import 'package:asnan/features/payments/domain/appointment_payment_status.dart';
import 'package:asnan/features/payments/domain/checkout.dart';
import 'package:asnan/features/payments/presentation/payment_confirmation_screen.dart';
import 'package:asnan/features/payments/presentation/payment_controller.dart';
import 'package:asnan/features/payments/presentation/payment_state.dart';
import 'package:dio/dio.dart';

import 'fakes/fake_secure_storage_service.dart';

Checkout _checkoutWithStatus(AppointmentPaymentStatus status) => Checkout(
      appointmentId: 'appt-1',
      paymentTransactionId: 'txn-1',
      providerSessionId: 'mock_sess_1',
      redirectUrl: '/api/v1/payments/mock/sessions/mock_sess_1/confirm',
      amount: 120,
      currency: 'USD',
      status: status,
    );

Widget _wrap({required PaymentState paymentState}) {
  final router = GoRouter(
    initialLocation: '/confirmation/doctor-1',
    routes: [
      GoRoute(
        path: '/home',
        name: AppRoutes.home,
        builder: (context, state) => const Scaffold(body: Text('Home screen')),
      ),
      GoRoute(
        path: '/booking/:id',
        name: AppRoutes.booking,
        builder: (context, state) => Scaffold(body: Text('Booking screen for ${state.pathParameters['id']}')),
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
  );
  container.read(paymentControllerProvider('doctor-1').notifier).state = paymentState;

  return UncontrolledProviderScope(
    container: container,
    child: MaterialApp.router(theme: AppTheme.light, routerConfig: router),
  );
}

void main() {
  testWidgets('with no checkout yet, shows a placeholder message', (tester) async {
    await tester.pumpWidget(_wrap(paymentState: const PaymentState()));
    await tester.pumpAndSettle();

    expect(find.text('No checkout in progress.'), findsOneWidget);
  });

  testWidgets('pending checkout shows the mock-provider handoff UI', (tester) async {
    await tester.pumpWidget(_wrap(paymentState: PaymentState(checkout: _checkoutWithStatus(AppointmentPaymentStatus.paymentPending))));
    await tester.pumpAndSettle();

    expect(find.text('Mock Payment Provider'), findsOneWidget);
    expect(find.text('Simulate Successful Payment'), findsOneWidget);
    expect(find.text('Simulate Failed Payment'), findsOneWidget);
  });

  testWidgets('polling shows a confirming indicator, not a result, while in flight', (tester) async {
    await tester.pumpWidget(_wrap(
      paymentState: PaymentState(checkout: _checkoutWithStatus(AppointmentPaymentStatus.paymentPending), isPolling: true),
    ));
    // Not pumpAndSettle: the indeterminate CircularProgressIndicator's
    // animation never settles on its own.
    await tester.pump();

    expect(find.text('Confirming your appointment...'), findsOneWidget);
    expect(find.text('Appointment confirmed'), findsNothing);
  });

  testWidgets('a poll timeout offers a manual recheck', (tester) async {
    await tester.pumpWidget(_wrap(
      paymentState: PaymentState(checkout: _checkoutWithStatus(AppointmentPaymentStatus.paymentPending), pollTimedOut: true),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Check again'), findsOneWidget);
  });

  testWidgets('Scheduled status shows the success state — never shown before the backend confirms it', (tester) async {
    await tester.pumpWidget(_wrap(paymentState: PaymentState(checkout: _checkoutWithStatus(AppointmentPaymentStatus.scheduled))));
    await tester.pumpAndSettle();

    expect(find.text('Appointment confirmed'), findsOneWidget);

    await tester.tap(find.text('Done'));
    await tester.pumpAndSettle();

    expect(find.text('Home screen'), findsOneWidget);
  });

  testWidgets('PaymentFailed status shows the failure state with a retry path back to booking', (tester) async {
    await tester.pumpWidget(_wrap(paymentState: PaymentState(checkout: _checkoutWithStatus(AppointmentPaymentStatus.paymentFailed))));
    await tester.pumpAndSettle();

    expect(find.text('Your payment could not be completed.'), findsOneWidget);

    await tester.tap(find.text('Try Again'));
    await tester.pumpAndSettle();

    expect(find.text('Booking screen for doctor-1'), findsOneWidget);
  });

  testWidgets('Expired status also shows the failure state', (tester) async {
    await tester.pumpWidget(_wrap(paymentState: PaymentState(checkout: _checkoutWithStatus(AppointmentPaymentStatus.expired))));
    await tester.pumpAndSettle();

    expect(find.text('Your payment could not be completed.'), findsOneWidget);
  });
}
