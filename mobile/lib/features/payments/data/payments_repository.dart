import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/checkout.dart';
import '../domain/mock_webhook_delivery.dart';
import '../domain/payment_failure.dart';
import '../domain/payment_result.dart';
import 'payments_api.dart';

class PaymentsRepository {
  PaymentsRepository(this._api);

  final PaymentsApi _api;

  Future<PaymentResult<Checkout>> checkout(String holdToken) => _guard(() => _api.checkout(holdToken));

  Future<PaymentResult<MockWebhookDelivery>> confirmMock(String redirectUrl, {required bool succeeded, String? failureReason}) =>
      _guard(() => _api.confirmMock(redirectUrl, succeeded: succeeded, failureReason: failureReason));

  Future<PaymentResult<void>> deliverMockWebhook(MockWebhookDelivery delivery) => _guard(() => _api.deliverMockWebhook(delivery));

  Future<PaymentResult<T>> _guard<T>(Future<T> Function() action) async {
    try {
      return PaymentSuccess(await action());
    } on DioException catch (e) {
      return PaymentError(PaymentFailure.fromDioException(e));
    }
  }
}

final paymentsRepositoryProvider = Provider<PaymentsRepository>((ref) => PaymentsRepository(ref.watch(paymentsApiProvider)));
