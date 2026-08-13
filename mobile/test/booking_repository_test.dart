import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/features/booking/data/booking_api.dart';
import 'package:asnan/features/booking/data/booking_repository.dart';
import 'package:asnan/features/booking/domain/booking_result.dart';

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

BookingApi _apiWithAdapter(Future<ResponseBody> Function(RequestOptions options) handler) {
  final dio = Dio(BaseOptions(baseUrl: 'http://test.local'));
  dio.httpClientAdapter = _StubHttpClientAdapter(handler);
  return BookingApi(dio);
}

ResponseBody _jsonBody(Object json, {int statusCode = 200}) => ResponseBody.fromString(
      jsonEncode(json),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

void main() {
  group('BookingRepository.getAvailability', () {
    test('maps a successful response to BookingSuccess with parsed slots', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.path, '/availability/doctors/doctor-1');
        expect(options.queryParameters['date'], '2026-09-01');
        return _jsonBody({
          'doctorId': 'doctor-1',
          'timeZoneId': 'Asia/Karachi',
          'date': '2026-09-01',
          'slots': [
            {'startUtc': '2026-09-01T04:00:00Z', 'endUtc': '2026-09-01T04:30:00Z'},
          ],
        });
      });
      final repository = BookingRepository(api);

      final result = await repository.getAvailability('doctor-1', DateTime(2026, 9, 1));

      switch (result) {
        case BookingSuccess(:final value):
          expect(value.slots, hasLength(1));
          expect(value.timeZoneId, 'Asia/Karachi');
        case BookingError():
          fail('expected success');
      }
    });
  });

  group('BookingRepository.createHold', () {
    test('maps a successful response to BookingSuccess with a hold token', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.path, '/appointments/holds');
        return _jsonBody({
          'id': 'hold-1',
          'doctorId': 'doctor-1',
          'slotStartUtc': '2026-09-01T04:00:00Z',
          'slotEndUtc': '2026-09-01T04:30:00Z',
          'holdToken': 'raw-token',
          'expiresAtUtc': '2026-09-01T04:05:00Z',
        }, statusCode: 201);
      });
      final repository = BookingRepository(api);

      final result = await repository.createHold(
        'doctor-1',
        DateTime.utc(2026, 9, 1, 4),
        DateTime.utc(2026, 9, 1, 4, 30),
      );

      switch (result) {
        case BookingSuccess(:final value):
          expect(value.holdToken, 'raw-token');
        case BookingError():
          fail('expected success');
      }
    });

    test('maps a 409 response to a conflict BookingError', () async {
      final api = _apiWithAdapter((options) async {
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
      final repository = BookingRepository(api);

      final result = await repository.createHold(
        'doctor-1',
        DateTime.utc(2026, 9, 1, 4),
        DateTime.utc(2026, 9, 1, 4, 30),
      );

      switch (result) {
        case BookingSuccess():
          fail('expected error');
        case BookingError(:final failure):
          expect(failure.isConflict, isTrue);
          expect(failure.message, 'Someone else just booked this slot.');
      }
    });
  });
}
