/// Values must match the backend's `DoctorSortBy` enum member names exactly
/// (sent as the `sortBy` query param). "Rating" is deliberately absent — no
/// review/rating system exists on either side yet.
enum DoctorSortBy {
  name('Name', 'Name (A-Z)'),
  fee('Fee', 'Consultation fee'),
  experience('Experience', 'Years of experience');

  const DoctorSortBy(this.apiValue, this.label);

  final String apiValue;
  final String label;
}
