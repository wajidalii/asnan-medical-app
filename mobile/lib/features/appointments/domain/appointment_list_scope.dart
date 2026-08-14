/// Values must match the backend's `AppointmentListScope` enum member names exactly (sent as the `scope` query param).
enum AppointmentListScope {
  upcoming('Upcoming'),
  past('Past');

  const AppointmentListScope(this.apiValue);

  final String apiValue;
}
