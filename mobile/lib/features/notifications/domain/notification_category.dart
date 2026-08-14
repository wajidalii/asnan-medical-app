/// Mirrors the backend's `NotificationCategory` enum (int-valued in JSON).
enum NotificationCategory {
  appointmentUpdates(1, 'Appointment updates'),
  paymentUpdates(2, 'Payment updates'),
  reminders(3, 'Appointment reminders'),
  chatMessages(4, 'Chat messages'),
  doctorAvailability(5, "Doctor availability changes");

  const NotificationCategory(this.value, this.label);

  final int value;

  /// Display label for the preferences screen.
  final String label;

  static NotificationCategory fromValue(int value) => NotificationCategory.values.firstWhere(
        (category) => category.value == value,
        orElse: () => throw ArgumentError('Unknown NotificationCategory value: $value'),
      );
}
