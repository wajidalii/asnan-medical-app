import '../../payments/domain/appointment_payment_status.dart';

/// Mirrors the backend's `RefundStatus` enum (int-valued in JSON).
enum RefundResultStatus {
  pending(1),
  succeeded(2),
  failed(3);

  const RefundResultStatus(this.value);

  final int value;

  static RefundResultStatus fromValue(int value) =>
      RefundResultStatus.values.firstWhere((s) => s.value == value, orElse: () => throw ArgumentError('Unknown RefundStatus value: $value'));
}

/// Mirrors the backend's `AppointmentCancellationDto` — the result of a
/// successful cancel call. [refundId]/[refundAmount]/[refundStatus] are all
/// null when the appointment had no captured payment to refund.
class AppointmentCancellation {
  const AppointmentCancellation({
    required this.appointmentId,
    required this.appointmentStatus,
    this.refundId,
    this.refundAmount,
    this.refundStatus,
  });

  final String appointmentId;
  final AppointmentPaymentStatus appointmentStatus;
  final String? refundId;
  final double? refundAmount;
  final RefundResultStatus? refundStatus;

  factory AppointmentCancellation.fromJson(Map<String, dynamic> json) => AppointmentCancellation(
        appointmentId: json['appointmentId'] as String,
        appointmentStatus: AppointmentPaymentStatus.fromValue(json['appointmentStatus'] as int),
        refundId: json['refundId'] as String?,
        refundAmount: (json['refundAmount'] as num?)?.toDouble(),
        refundStatus: json['refundStatus'] == null ? null : RefundResultStatus.fromValue(json['refundStatus'] as int),
      );
}
