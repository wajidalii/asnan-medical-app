import '../domain/appointment_summary.dart';
import '../domain/appointments_failure.dart';
import '../domain/cancellation_preview.dart';

class _Unset {
  const _Unset();
}

const _unset = _Unset();

class AppointmentDetailsState {
  const AppointmentDetailsState({
    this.appointment,
    this.isLoadingPreview = false,
    this.preview,
    this.previewFailure,
    this.isCancelling = false,
    this.cancelFailure,
    this.cancelled = false,
  });

  final AppointmentSummary? appointment;

  final bool isLoadingPreview;
  final CancellationPreview? preview;
  final AppointmentsFailure? previewFailure;

  final bool isCancelling;
  final AppointmentsFailure? cancelFailure;
  final bool cancelled;

  AppointmentDetailsState copyWith({
    Object? appointment = _unset,
    bool? isLoadingPreview,
    Object? preview = _unset,
    Object? previewFailure = _unset,
    bool? isCancelling,
    Object? cancelFailure = _unset,
    bool? cancelled,
  }) {
    return AppointmentDetailsState(
      appointment: identical(appointment, _unset) ? this.appointment : appointment as AppointmentSummary?,
      isLoadingPreview: isLoadingPreview ?? this.isLoadingPreview,
      preview: identical(preview, _unset) ? this.preview : preview as CancellationPreview?,
      previewFailure: identical(previewFailure, _unset) ? this.previewFailure : previewFailure as AppointmentsFailure?,
      isCancelling: isCancelling ?? this.isCancelling,
      cancelFailure: identical(cancelFailure, _unset) ? this.cancelFailure : cancelFailure as AppointmentsFailure?,
      cancelled: cancelled ?? this.cancelled,
    );
  }
}
