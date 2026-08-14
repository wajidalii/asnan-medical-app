import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/payments_repository.dart';
import '../domain/appointment_payment_status.dart';
import '../domain/checkout.dart';
import '../domain/mock_webhook_delivery.dart';
import '../domain/payment_result.dart';
import 'payment_state.dart';

const _pollInterval = Duration(seconds: 2);
const _maxPollAttempts = 15;

/// Checkout -> mock-provider handoff -> poll-for-Scheduled, scoped per
/// doctor (family, same convention as BookingController). Never trusts the
/// mock-confirm/webhook-relay calls' own success to mean "the appointment
/// is booked" — [checkout]'s status only ever changes via [_pollOnce]
/// re-fetching it from the backend, per ARCHITECTURE.md §8.
class PaymentController extends FamilyNotifier<PaymentState, String> {
  Timer? _pollTimer;
  int _pollAttempts = 0;
  String? _holdToken;

  @override
  PaymentState build(String doctorId) {
    ref.onDispose(() => _pollTimer?.cancel());
    return const PaymentState();
  }

  Future<void> startCheckout(String holdToken) async {
    _holdToken = holdToken;
    state = state.copyWith(isCheckingOut: true, checkoutFailure: null);

    final result = await ref.read(paymentsRepositoryProvider).checkout(holdToken);

    switch (result) {
      case PaymentSuccess<Checkout>(:final value):
        state = state.copyWith(isCheckingOut: false, checkout: value);
      case PaymentError<Checkout>(:final failure):
        state = state.copyWith(isCheckingOut: false, checkoutFailure: failure);
    }
  }

  /// Dev/staging-only: stands in for a real provider's own hosted checkout
  /// UI + asynchronous webhook delivery (#19/#22).
  Future<void> submitMockOutcome({required bool succeeded, String? failureReason}) async {
    final checkout = state.checkout;
    if (checkout == null) return;

    state = state.copyWith(isSubmittingMockOutcome: true, mockOutcomeFailure: null);
    final repo = ref.read(paymentsRepositoryProvider);

    final confirmResult = await repo.confirmMock(checkout.redirectUrl, succeeded: succeeded, failureReason: failureReason);
    if (confirmResult is PaymentError<MockWebhookDelivery>) {
      state = state.copyWith(isSubmittingMockOutcome: false, mockOutcomeFailure: confirmResult.failure);
      return;
    }
    final delivery = (confirmResult as PaymentSuccess<MockWebhookDelivery>).value;

    final webhookResult = await repo.deliverMockWebhook(delivery);
    switch (webhookResult) {
      case PaymentSuccess<void>():
        state = state.copyWith(isSubmittingMockOutcome: false);
        _startPolling();
      case PaymentError<void>(:final failure):
        state = state.copyWith(isSubmittingMockOutcome: false, mockOutcomeFailure: failure);
    }
  }

  void _startPolling() {
    _pollAttempts = 0;
    state = state.copyWith(isPolling: true, pollTimedOut: false);
    _pollTimer?.cancel();
    _pollTimer = Timer.periodic(_pollInterval, (_) => _pollOnce());
    _pollOnce();
  }

  /// Manual re-check after [pollTimedOut] — same underlying poll, just user-triggered.
  void retryPolling() => _startPolling();

  Future<void> _pollOnce() async {
    final holdToken = _holdToken;
    if (holdToken == null) return;

    _pollAttempts++;
    final result = await ref.read(paymentsRepositoryProvider).checkout(holdToken);

    switch (result) {
      case PaymentSuccess<Checkout>(:final value):
        state = state.copyWith(checkout: value);
        if (value.status != AppointmentPaymentStatus.paymentPending) {
          _pollTimer?.cancel();
          state = state.copyWith(isPolling: false);
        } else if (_pollAttempts >= _maxPollAttempts) {
          _pollTimer?.cancel();
          state = state.copyWith(isPolling: false, pollTimedOut: true);
        }
      case PaymentError<Checkout>():
        // Transient hiccup while polling — keep trying until the attempt cap.
        if (_pollAttempts >= _maxPollAttempts) {
          _pollTimer?.cancel();
          state = state.copyWith(isPolling: false, pollTimedOut: true);
        }
    }
  }
}

final paymentControllerProvider = NotifierProvider.family<PaymentController, PaymentState, String>(PaymentController.new);
