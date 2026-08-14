import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/dio_client.dart';
import '../domain/checkout.dart';
import '../domain/mock_webhook_delivery.dart';

/// Thin wrapper over the raw HTTP calls — same convention as BookingApi.
class PaymentsApi {
  PaymentsApi(this._dio);

  final Dio _dio;

  Future<Checkout> checkout(String holdToken) async {
    final response = await _dio.post<Map<String, dynamic>>('/payments/checkout', data: {'holdToken': holdToken});
    return Checkout.fromJson(response.data!);
  }

  /// Dev/staging-only mock-provider handoff (#19/#22): simulates the
  /// provider settling the checkout session. [redirectUrl] is the
  /// server-provided confirm path (already `/api/v1/...`-prefixed); the
  /// leading version segment is stripped since [Dio]'s baseUrl already
  /// includes it, same as every other relative call in this app.
  Future<MockWebhookDelivery> confirmMock(String redirectUrl, {required bool succeeded, String? failureReason}) async {
    final path = redirectUrl.startsWith('/api/v1') ? redirectUrl.substring('/api/v1'.length) : redirectUrl;
    final response = await _dio.post<Map<String, dynamic>>(
      path,
      data: {'succeeded': succeeded, 'failureReason': failureReason},
    );
    return MockWebhookDelivery.fromJson(response.data!);
  }

  /// Relays the mock provider's signed payload to the real webhook endpoint
  /// — exactly what a real provider would deliver on its own, so the
  /// checkout->scheduled path is exercised faithfully end to end.
  Future<void> deliverMockWebhook(MockWebhookDelivery delivery) async {
    await _dio.post<void>(
      '/payments/webhook',
      data: delivery.rawBody,
      options: Options(
        contentType: Headers.jsonContentType,
        headers: {'X-Mock-Signature': delivery.signatureHeaderValue},
      ),
    );
  }
}

final paymentsApiProvider = Provider<PaymentsApi>((ref) => PaymentsApi(ref.watch(dioProvider)));
