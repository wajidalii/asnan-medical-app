import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/appointments_repository.dart';
import '../domain/appointment_cancellation.dart';
import '../domain/appointment_summary.dart';
import '../domain/appointments_result.dart';
import '../domain/cancellation_preview.dart';
import 'appointment_details_state.dart';

/// Scoped per appointment id — the screen calls [initialize] once from
/// initState with the summary it already has (from the list it came from),
/// same convention as BookingController.loadDateStrip() being an explicit
/// call rather than fetched inside build().
class AppointmentDetailsController extends FamilyNotifier<AppointmentDetailsState, String> {
  @override
  AppointmentDetailsState build(String appointmentId) => const AppointmentDetailsState();

  void initialize(AppointmentSummary appointment) {
    state = state.copyWith(appointment: appointment);
  }

  /// Called before showing the cancellation confirmation — the applicable
  /// refund policy must be shown before the user confirms, never assumed.
  Future<void> loadCancellationPreview() async {
    final appointmentId = state.appointment?.id;
    if (appointmentId == null) return;

    state = state.copyWith(isLoadingPreview: true, previewFailure: null, preview: null);

    final result = await ref.read(appointmentsRepositoryProvider).previewCancellation(appointmentId);

    switch (result) {
      case AppointmentsSuccess<CancellationPreview>(:final value):
        state = state.copyWith(isLoadingPreview: false, preview: value);
      case AppointmentsError<CancellationPreview>(:final failure):
        state = state.copyWith(isLoadingPreview: false, previewFailure: failure);
    }
  }

  Future<void> confirmCancellation(String? reason) async {
    final appointment = state.appointment;
    if (appointment == null) return;

    state = state.copyWith(isCancelling: true, cancelFailure: null);

    final result = await ref.read(appointmentsRepositoryProvider).cancel(appointment.id, reason);

    switch (result) {
      case AppointmentsSuccess<AppointmentCancellation>(:final value):
        state = state.copyWith(
          isCancelling: false,
          cancelled: true,
          appointment: appointment.copyWith(status: value.appointmentStatus),
        );
      case AppointmentsError<AppointmentCancellation>(:final failure):
        state = state.copyWith(isCancelling: false, cancelFailure: failure);
    }
  }

  void dismissCancellationPreview() {
    state = state.copyWith(preview: null, previewFailure: null);
  }
}

final appointmentDetailsControllerProvider =
    NotifierProvider.family<AppointmentDetailsController, AppointmentDetailsState, String>(AppointmentDetailsController.new);
