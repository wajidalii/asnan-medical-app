import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/features/payments/data/payments_api.dart';
import 'package:asnan/features/payments/data/payments_repository.dart';
import 'package:asnan/features/payments/domain/appointment_payment_status.dart';
import 'package:asnan/features/payments/domain/mock_webhook_delivery.dart';
import 'package:asnan/features/payments/domain/payment_result.dart';

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

PaymentsApi _apiWithAdapter(Future<ResponseBody> Function(RequestOptions options) handler) {
  final dio = Dio(BaseOptions(baseUrl: 'http://test.local/api/v1'));
  dio.httpClientAdapter = _StubHttpClientAdapter(handler);
  return PaymentsApi(dio);
}

ResponseBody _jsonBody(Object json, {int statusCode = 200}) => ResponseBody.fromString(
      jsonEncode(json),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

Map<String, dynamic> _checkoutJson({int status = 1}) => {
      'appointmentId': 'appt-1',
      'paymentTransactionId': 'txn-1',
      'providerSessionId': 'mock_sess_1',
      'redirectUrl': '/api/v1/payments/mock/sessions/mock_sess_1/confirm',
      'amount': 120.0,
      'currency': 'USD',
      'status': status,
    };

void main() {
  group('PaymentsRepository.checkout', () {
    test('maps a successful response to PaymentSuccess with parsed status', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.path, '/payments/checkout');
        expect(options.data, {'holdToken': 'raw-token'});
        return _jsonBody(_checkoutJson());
      });
      final repository = PaymentsRepository(api);

      final result = await repository.checkout('raw-token');

      switch (result) {
        case PaymentSuccess(:final value):
          expect(value.appointmentId, 'appt-1');
          expect(value.status, AppointmentPaymentStatus.paymentPending);
        case PaymentError():
          fail('expected success');
      }
    });

    test('a Scheduled status (2) parses correctly — the poll target', () async {
      final api = _apiWithAdapter((options) async => _jsonBody(_checkoutJson(status: 2)));
      final repository = PaymentsRepository(api);

      final result = await repository.checkout('raw-token');

      switch (result) {
        case PaymentSuccess(:final value):
          expect(value.status, AppointmentPaymentStatus.scheduled);
        case PaymentError():
          fail('expected success');
      }
    });

    test('maps a 404 response to a not-found PaymentError', () async {
      final api = _apiWithAdapter((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 404, data: {'title': 'Not found.'}),
        );
      });
      final repository = PaymentsRepository(api);

      final result = await repository.checkout('unknown-token');

      switch (result) {
        case PaymentSuccess():
          fail('expected error');
        case PaymentError(:final failure):
          expect(failure.isNotFound, isTrue);
      }
    });
  });

  group('PaymentsRepository.confirmMock', () {
    test('strips the /api/v1 prefix from redirectUrl before posting', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.path, '/payments/mock/sessions/mock_sess_1/confirm');
        expect(options.data, {'succeeded': true, 'failureReason': null});
        return _jsonBody({
          'providerEventId': 'evt-1',
          'rawBody': '{"eventId":"evt-1"}',
          'signatureHeaderValue': 'deadbeef',
        });
      });
      final repository = PaymentsRepository(api);

      final result = await repository.confirmMock(
        '/api/v1/payments/mock/sessions/mock_sess_1/confirm',
        succeeded: true,
      );

      switch (result) {
        case PaymentSuccess(:final value):
          expect(value.signatureHeaderValue, 'deadbeef');
        case PaymentError():
          fail('expected success');
      }
    });
  });

  group('PaymentsRepository.deliverMockWebhook', () {
    test('posts the raw body with the signature header, succeeds with no content', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.path, '/payments/webhook');
        expect(options.data, '{"eventId":"evt-1"}');
        expect(options.headers['X-Mock-Signature'], 'deadbeef');
        return ResponseBody.fromString('', 200);
      });
      final repository = PaymentsRepository(api);

      const delivery = MockWebhookDelivery(
        providerEventId: 'evt-1',
        rawBody: '{"eventId":"evt-1"}',
        signatureHeaderValue: 'deadbeef',
      );
      final result = await repository.deliverMockWebhook(delivery);

      switch (result) {
        case PaymentSuccess():
          break;
        case PaymentError():
          fail('expected success');
      }
    });
  });
}
