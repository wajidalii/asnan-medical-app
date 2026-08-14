/// App-level event payload — deliberately not the device_calendar package's
/// own `Event` type, so CalendarService stays testable without touching
/// platform channels (see CalendarService's doc comment).
class CalendarEventData {
  const CalendarEventData({
    required this.title,
    required this.startUtc,
    required this.endUtc,
    this.location,
    this.description,
  });

  final String title;
  final DateTime startUtc;
  final DateTime endUtc;

  /// Clinic address, if known — not the doctor's specialty/qualifications or any medical detail.
  final String? location;

  /// A minimal note only — no diagnosis/visit-reason text, matching the
  /// no-sensitive-info-in-notification stance applied elsewhere (ARCHITECTURE.md §10/§11).
  final String? description;
}
