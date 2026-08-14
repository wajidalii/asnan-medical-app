import '../../payments/domain/appointment_payment_status.dart';

/// Mirrors the backend's `AppointmentSummaryDto` — reuses
/// AppointmentPaymentStatus (features/payments) rather than duplicating the
/// same 11-value enum a second time.
class AppointmentSummary {
  const AppointmentSummary({
    required this.id,
    required this.doctorProfileId,
    required this.doctorFullName,
    required this.slotStartUtc,
    required this.slotEndUtc,
    required this.status,
    required this.consultationFee,
    required this.currency,
    this.chatConversationId,
  });

  final String id;
  final String doctorProfileId;
  final String doctorFullName;
  final DateTime slotStartUtc;
  final DateTime slotEndUtc;
  final AppointmentPaymentStatus status;
  final double consultationFee;
  final String currency;

  /// Null until the appointment has reached (or passed through) Scheduled — a
  /// ChatConversation is only ever created at that point (backend #20).
  final String? chatConversationId;

  AppointmentSummary copyWith({AppointmentPaymentStatus? status}) => AppointmentSummary(
        id: id,
        doctorProfileId: doctorProfileId,
        doctorFullName: doctorFullName,
        slotStartUtc: slotStartUtc,
        slotEndUtc: slotEndUtc,
        status: status ?? this.status,
        consultationFee: consultationFee,
        currency: currency,
        chatConversationId: chatConversationId,
      );

  factory AppointmentSummary.fromJson(Map<String, dynamic> json) => AppointmentSummary(
        id: json['id'] as String,
        doctorProfileId: json['doctorProfileId'] as String,
        doctorFullName: json['doctorFullName'] as String,
        slotStartUtc: DateTime.parse(json['slotStartUtc'] as String),
        slotEndUtc: DateTime.parse(json['slotEndUtc'] as String),
        status: AppointmentPaymentStatus.fromValue(json['status'] as int),
        consultationFee: (json['consultationFee'] as num).toDouble(),
        currency: json['currency'] as String,
        chatConversationId: json['chatConversationId'] as String?,
      );
}
