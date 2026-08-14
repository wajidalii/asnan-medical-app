/// Mirrors the backend's `AppointmentStatus` enum (int-valued in JSON — no
/// string-enum converter configured server-side) — only the values this
/// screen actually branches on need names here; the rest still round-trip
/// correctly via their raw value.
enum AppointmentPaymentStatus {
  paymentPending(1),
  scheduled(2),
  completed(3),
  noShow(4),
  cancelledByPatient(5),
  cancelledByDoctor(6),
  cancelledByAdmin(7),
  refundPending(8),
  refunded(9),
  paymentFailed(10),
  expired(11);

  const AppointmentPaymentStatus(this.value);

  final int value;

  static AppointmentPaymentStatus fromValue(int value) => AppointmentPaymentStatus.values.firstWhere(
        (status) => status.value == value,
        orElse: () => throw ArgumentError('Unknown AppointmentStatus value: $value'),
      );
}
