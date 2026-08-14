import 'appointment_payment_status.dart';

/// Mirrors the backend's `CheckoutDto` — returned by POST /payments/checkout
/// and re-fetched by re-calling the same (idempotent) endpoint to poll
/// [status] rather than trusting client-reported payment success.
class Checkout {
  const Checkout({
    required this.appointmentId,
    required this.paymentTransactionId,
    required this.providerSessionId,
    required this.redirectUrl,
    required this.amount,
    required this.currency,
    required this.status,
  });

  final String appointmentId;
  final String paymentTransactionId;
  final String providerSessionId;
  final String redirectUrl;
  final double amount;
  final String currency;
  final AppointmentPaymentStatus status;

  factory Checkout.fromJson(Map<String, dynamic> json) => Checkout(
        appointmentId: json['appointmentId'] as String,
        paymentTransactionId: json['paymentTransactionId'] as String,
        providerSessionId: json['providerSessionId'] as String,
        redirectUrl: json['redirectUrl'] as String,
        amount: (json['amount'] as num).toDouble(),
        currency: json['currency'] as String,
        status: AppointmentPaymentStatus.fromValue(json['status'] as int),
      );
}
