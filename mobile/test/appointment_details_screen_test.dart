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
import 'package:asnan/features/appointments/domain/appointment_summary.dart';
import 'package:asnan/features/appointments/presentation/appointment_details_screen.dart';
import 'package:asnan/features/calendar/calendar_event_data.dart';
import 'package:asnan/features/calendar/calendar_service.dart';
import 'package:asnan/features/calendar/calendar_write_result.dart';
import 'package:asnan/features/doctors/domain/doctor_detail.dart';
import 'package:asnan/features/doctors/presentation/doctor_detail_controller.dart';
import 'package:asnan/features/payments/domain/appointment_payment_status.dart';

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

/// The test double the issue's testing requirement calls for — records the
/// payload shape CalendarService.addEvent was called with, no platform channels.
class FakeCalendarService implements CalendarService {
  CalendarEventData? lastEvent;
  CalendarWriteResult nextResult = const CalendarWriteResult(CalendarWriteStatus.success, eventId: 'evt-1');

  @override
  Future<CalendarWriteResult> addEvent(CalendarEventData data) async {
    lastEvent = data;
    return nextResult;
  }
}

DoctorDetail _doctor() => const DoctorDetail(
      id: 'doctor-1',
      fullName: 'Dr. Jane Details',
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

AppointmentSummary _appointment({AppointmentPaymentStatus status = AppointmentPaymentStatus.scheduled}) => AppointmentSummary(
      id: 'appt-1',
      doctorProfileId: 'doctor-1',
      doctorFullName: 'Dr. Jane Details',
      slotStartUtc: DateTime.utc(2026, 9, 1, 9),
      slotEndUtc: DateTime.utc(2026, 9, 1, 9, 30),
      status: status,
      consultationFee: 120,
      currency: 'USD',
    );

Widget _wrap(HttpClientAdapter adapter, AppointmentSummary appointment, {FakeCalendarService? calendarService}) {
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
      doctorDetailProvider('doctor-1').overrideWith((ref) async => _doctor()),
      if (calendarService != null) calendarServiceProvider.overrideWithValue(calendarService),
    ],
    child: MaterialApp(theme: AppTheme.light, home: AppointmentDetailsScreen(appointment: appointment)),
  );
}

void main() {
  testWidgets('shows doctor, status, date/time, location, and fee', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async => _json({}));

    await tester.pumpWidget(_wrap(adapter, _appointment()));
    await tester.pumpAndSettle();

    expect(find.text('Dr. Jane Details'), findsOneWidget);
    expect(find.text('Scheduled'), findsOneWidget);
    expect(find.text('123 Health St'), findsOneWidget);
    expect(find.text('USD 120.00'), findsOneWidget);
  });

  testWidgets('for a non-Scheduled appointment, hides cancel/calendar/chat actions', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async => _json({}));

    await tester.pumpWidget(_wrap(adapter, _appointment(status: AppointmentPaymentStatus.completed)));
    await tester.pumpAndSettle();

    expect(find.text('Completed'), findsOneWidget);
    expect(find.text('Cancel Appointment'), findsNothing);
    expect(find.text('Add to Calendar'), findsNothing);
    expect(find.text('Message Doctor'), findsNothing);
  });

  testWidgets('cancelling well in advance shows the refund policy then confirms', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path.endsWith('/cancellation-preview')) {
        return _json({'appointmentId': 'appt-1', 'isAllowed': true, 'refundPercentage': 100, 'refundAmount': 120.0, 'currency': 'USD'});
      }
      if (options.path.endsWith('/cancel')) {
        return _json({'appointmentId': 'appt-1', 'appointmentStatus': 9, 'refundId': 'refund-1', 'refundAmount': 120.0, 'refundStatus': 2});
      }
      return _json({});
    });

    await tester.pumpWidget(_wrap(adapter, _appointment()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Cancel Appointment'));
    await tester.pumpAndSettle();

    expect(find.textContaining("You'll receive a refund of USD 120.00"), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, 'Cancel appointment'));
    await tester.pumpAndSettle();

    expect(find.text('Appointment cancelled.'), findsOneWidget);
    expect(find.text('Refunded'), findsOneWidget);
  });

  testWidgets('cancelling too close to the appointment shows the window-closed message with no confirm action', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      if (options.path.endsWith('/cancellation-preview')) {
        return _json({'appointmentId': 'appt-1', 'isAllowed': false, 'refundPercentage': 0, 'refundAmount': 0.0, 'currency': 'USD'});
      }
      return _json({});
    });

    await tester.pumpWidget(_wrap(adapter, _appointment()));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Cancel Appointment'));
    await tester.pumpAndSettle();

    expect(find.text('This appointment is too close to its scheduled time to cancel.'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Cancel appointment'), findsNothing);

    await tester.tap(find.text('Keep appointment'));
    await tester.pumpAndSettle();

    // Still Scheduled — nothing was cancelled.
    expect(find.text('Scheduled'), findsOneWidget);
  });

  testWidgets('Add to Calendar passes the correct event payload to CalendarService', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async => _json({}));
    final fakeCalendar = FakeCalendarService();

    await tester.pumpWidget(_wrap(adapter, _appointment(), calendarService: fakeCalendar));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Add to Calendar'));
    await tester.pumpAndSettle();

    expect(fakeCalendar.lastEvent, isNotNull);
    expect(fakeCalendar.lastEvent!.title, 'Appointment with Dr. Jane Details');
    expect(fakeCalendar.lastEvent!.startUtc, DateTime.utc(2026, 9, 1, 9));
    expect(fakeCalendar.lastEvent!.endUtc, DateTime.utc(2026, 9, 1, 9, 30));
    expect(fakeCalendar.lastEvent!.location, '123 Health St');
    expect(find.text('Added to your calendar.'), findsOneWidget);
  });

  testWidgets('Add to Calendar surfaces a permission-denied message', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async => _json({}));
    final fakeCalendar = FakeCalendarService()..nextResult = const CalendarWriteResult(CalendarWriteStatus.permissionDenied);

    await tester.pumpWidget(_wrap(adapter, _appointment(), calendarService: fakeCalendar));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Add to Calendar'));
    await tester.pumpAndSettle();

    expect(find.text('Calendar permission was not granted.'), findsOneWidget);
  });
}
