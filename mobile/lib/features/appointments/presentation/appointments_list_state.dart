import '../domain/appointment_summary.dart';
import '../domain/appointments_failure.dart';

class AppointmentsListState {
  const AppointmentsListState({
    this.isLoading = true,
    this.isLoadingMore = false,
    this.appointments = const [],
    this.failure,
    this.hasMore = false,
    this.page = 1,
  });

  final bool isLoading;
  final bool isLoadingMore;
  final List<AppointmentSummary> appointments;
  final AppointmentsFailure? failure;
  final bool hasMore;
  final int page;

  bool get isEmpty => !isLoading && failure == null && appointments.isEmpty;
}
