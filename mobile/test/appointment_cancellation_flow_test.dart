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
import 'package:asnan/features/appointments/domain/appointment_summary.dart';
import 'package:asnan/features/appointments/presentation/appointment_details_screen.dart';
import 'package:asnan/features/appointments/presentation/appointments_list_screen.dart';

import 'fakes/fake_secure_storage_service.dart';

/// Critical-flow coverage (issue #37): appointments list -> details ->
/// cancel, driving real screens through real GoRouter navigation (the
/// AppointmentSummary is handed across via route `extra`, exactly as
/// AppointmentsListScreen's tile does — see its onTap). Existing
/// appointment_details_screen_test.dart tests the cancel action in
/// isolation with a pre-built AppointmentSummary; this proves the same
/// action also works reached via a real list-tile tap and real navigation.
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

Map<String, dynamic> _appointmentJson() => {
      'id': 'appt-1',
      'doctorProfileId': 'doctor-1',
      'doctorFullName': 'Dr. Cancel Test',
      'slotStartUtc': DateTime.now().toUtc().add(const Duration(days: 10)).toIso8601String(),
      'slotEndUtc': DateTime.now().toUtc().add(const Duration(days: 10, minutes: 30)).toIso8601String(),
      'status': 2, // Scheduled — the only cancellable status.
      'consultationFee': 120.0,
      'currency': 'USD',
      'chatConversationId': null,
    };

Map<String, dynamic> _doctorDetailJson() => {
      'id': 'doctor-1',
      'fullName': 'Dr. Cancel Test',
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

Widget _wrap(HttpClientAdapter adapter) {
  final router = GoRouter(
    initialLocation: '/appointments',
    routes: [
      GoRoute(
        path: '/appointments',
        name: AppRoutes.appointments,
        builder: (context, state) => const AppointmentsListScreen(),
        routes: [
          GoRoute(
            path: 'details',
            name: AppRoutes.appointmentDetails,
            builder: (context, state) => AppointmentDetailsScreen(appointment: state.extra! as AppointmentSummary),
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
  testWidgets('appointments list -> details -> cancel shows the refund preview then confirms cancellation', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path == '/appointments') {
        return _json({'items': [_appointmentJson()], 'page': 1, 'pageSize': 20, 'totalCount': 1});
      }
      if (options.path == '/doctors/doctor-1') return _json(_doctorDetailJson());
      if (options.path == '/appointments/appt-1/cancellation-preview') {
        return _json({
          'appointmentId': 'appt-1',
          'isAllowed': true,
          'refundPercentage': 100,
          'refundAmount': 120.0,
          'currency': 'USD',
        });
      }
      if (options.path == '/appointments/appt-1/cancel') {
        return _json({
          'appointmentId': 'appt-1',
          'appointmentStatus': 5, // CancelledByPatient
          'refundId': 'refund-1',
          'refundAmount': 120.0,
          'refundStatus': 1, // Pending
        });
      }

      throw DioException(requestOptions: options, type: DioExceptionType.connectionError);
    });

    await tester.pumpWidget(_wrap(adapter));
    await tester.pumpAndSettle();

    expect(find.text('Dr. Cancel Test'), findsOneWidget);

    // List -> details.
    await tester.tap(find.text('Dr. Cancel Test'));
    await tester.pumpAndSettle();
    expect(find.text('Appointment Details'), findsOneWidget);
    expect(find.text('Scheduled'), findsOneWidget);

    // Load the cancellation preview -> confirmation dialog.
    await tester.tap(find.widgetWithText(OutlinedButton, 'Cancel Appointment'));
    await tester.pumpAndSettle();
    expect(find.text("You'll receive a refund of USD 120.00 (100% of the consultation fee)."), findsOneWidget);

    // Confirm -> cancel API call -> success snackbar.
    await tester.tap(find.widgetWithText(FilledButton, 'Cancel appointment'));
    await tester.pumpAndSettle();

    expect(find.text('Appointment cancelled.'), findsOneWidget);
    expect(find.text('Cancelled by you'), findsOneWidget);
  });
}
