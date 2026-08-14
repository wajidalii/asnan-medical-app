enum CalendarWriteStatus {
  success,
  permissionDenied,
  noWritableCalendar,
  failure,
}

class CalendarWriteResult {
  const CalendarWriteResult(this.status, {this.eventId});

  final CalendarWriteStatus status;
  final String? eventId;
}
