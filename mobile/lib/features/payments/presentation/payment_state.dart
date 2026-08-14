import '../domain/checkout.dart';
import '../domain/payment_failure.dart';

class _Unset {
  const _Unset();
}

const _unset = _Unset();

class PaymentState {
  const PaymentState({
    this.isCheckingOut = false,
    this.checkout,
    this.checkoutFailure,
    this.isSubmittingMockOutcome = false,
    this.mockOutcomeFailure,
    this.isPolling = false,
    this.pollTimedOut = false,
  });

  final bool isCheckingOut;
  final Checkout? checkout;
  final PaymentFailure? checkoutFailure;

  final bool isSubmittingMockOutcome;
  final PaymentFailure? mockOutcomeFailure;

  final bool isPolling;
  final bool pollTimedOut;

  PaymentState copyWith({
    bool? isCheckingOut,
    Object? checkout = _unset,
    Object? checkoutFailure = _unset,
    bool? isSubmittingMockOutcome,
    Object? mockOutcomeFailure = _unset,
    bool? isPolling,
    bool? pollTimedOut,
  }) {
    return PaymentState(
      isCheckingOut: isCheckingOut ?? this.isCheckingOut,
      checkout: identical(checkout, _unset) ? this.checkout : checkout as Checkout?,
      checkoutFailure: identical(checkoutFailure, _unset) ? this.checkoutFailure : checkoutFailure as PaymentFailure?,
      isSubmittingMockOutcome: isSubmittingMockOutcome ?? this.isSubmittingMockOutcome,
      mockOutcomeFailure: identical(mockOutcomeFailure, _unset) ? this.mockOutcomeFailure : mockOutcomeFailure as PaymentFailure?,
      isPolling: isPolling ?? this.isPolling,
      pollTimedOut: pollTimedOut ?? this.pollTimedOut,
    );
  }
}
