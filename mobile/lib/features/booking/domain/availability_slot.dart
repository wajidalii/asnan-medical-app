/// UTC instants as returned by the backend — always converted with
/// `.toLocal()` before display, never re-derived client-side
/// (ARCHITECTURE.md §6: backend is the sole source of truth for availability).
class AvailabilitySlot {
  const AvailabilitySlot({required this.startUtc, required this.endUtc});

  final DateTime startUtc;
  final DateTime endUtc;

  factory AvailabilitySlot.fromJson(Map<String, dynamic> json) => AvailabilitySlot(
        startUtc: DateTime.parse(json['startUtc'] as String),
        endUtc: DateTime.parse(json['endUtc'] as String),
      );
}
