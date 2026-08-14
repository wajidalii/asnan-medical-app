import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/features/appointments/data/appointments_api.dart';
import 'package:asnan/features/appointments/data/appointments_repository.dart';
import 'package:asnan/features/appointments/domain/appointment_list_scope.dart';
import 'package:asnan/features/appointments/domain/appointments_result.dart';
import 'package:asnan/features/payments/domain/appointment_payment_status.dart';

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

AppointmentsApi _apiWithAdapter(Future<ResponseBody> Function(RequestOptions options) handler) {
  final dio = Dio(BaseOptions(baseUrl: 'http://test.local'));
  dio.httpClientAdapter = _StubHttpClientAdapter(handler);
  return AppointmentsApi(dio);
}

ResponseBody _jsonBody(Object json, {int statusCode = 200}) => ResponseBody.fromString(
      jsonEncode(json),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

void main() {
  group('AppointmentsRepository.list', () {
    test('sends the scope as the backend enum member name and parses items', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.path, '/appointments');
        expect(options.queryParameters['scope'], 'Upcoming');
        return _jsonBody({
          'items': [
            {
              'id': 'appt-1',
              'doctorProfileId': 'doctor-1',
              'doctorFullName': 'Dr. Jane',
              'slotStartUtc': '2026-09-01T09:00:00Z',
              'slotEndUtc': '2026-09-01T09:30:00Z',
              'status': 2,
              'consultationFee': 120.0,
              'currency': 'USD',
            },
          ],
          'page': 1,
          'pageSize': 20,
          'totalCount': 1,
        });
      });
      final repository = AppointmentsRepository(api);

      final result = await repository.list(AppointmentListScope.upcoming);

      switch (result) {
        case AppointmentsSuccess(:final value):
          expect(value.items, hasLength(1));
          expect(value.items.first.status, AppointmentPaymentStatus.scheduled);
        case AppointmentsError():
          fail('expected success');
      }
    });
  });

  group('AppointmentsRepository.previewCancellation', () {
    test('maps a successful response', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.path, '/appointments/appt-1/cancellation-preview');
        return _jsonBody({'appointmentId': 'appt-1', 'isAllowed': true, 'refundPercentage': 100, 'refundAmount': 120.0, 'currency': 'USD'});
      });
      final repository = AppointmentsRepository(api);

      final result = await repository.previewCancellation('appt-1');

      switch (result) {
        case AppointmentsSuccess(:final value):
          expect(value.isAllowed, isTrue);
          expect(value.refundPercentage, 100);
        case AppointmentsError():
          fail('expected success');
      }
    });

    test('maps a 409 response to a conflict error', () async {
      final api = _apiWithAdapter((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 409, data: {'title': 'Only a Scheduled appointment can be cancelled.'}),
        );
      });
      final repository = AppointmentsRepository(api);

      final result = await repository.previewCancellation('appt-1');

      switch (result) {
        case AppointmentsSuccess():
          fail('expected error');
        case AppointmentsError(:final failure):
          expect(failure.isConflict, isTrue);
      }
    });
  });

  group('AppointmentsRepository.cancel', () {
    test('maps a successful response', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.path, '/appointments/appt-1/cancel');
        expect(options.data, {'reason': 'Change of plans'});
        return _jsonBody({'appointmentId': 'appt-1', 'appointmentStatus': 9, 'refundId': 'refund-1', 'refundAmount': 120.0, 'refundStatus': 2});
      });
      final repository = AppointmentsRepository(api);

      final result = await repository.cancel('appt-1', 'Change of plans');

      switch (result) {
        case AppointmentsSuccess(:final value):
          expect(value.appointmentStatus, AppointmentPaymentStatus.refunded);
          expect(value.refundAmount, 120.0);
        case AppointmentsError():
          fail('expected success');
      }
    });

    test('maps a 403 response to a forbidden error', () async {
      final api = _apiWithAdapter((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 403),
        );
      });
      final repository = AppointmentsRepository(api);

      final result = await repository.cancel('appt-1', null);

      switch (result) {
        case AppointmentsSuccess():
          fail('expected error');
        case AppointmentsError(:final failure):
          expect(failure.isForbidden, isTrue);
      }
    });
  });
}
