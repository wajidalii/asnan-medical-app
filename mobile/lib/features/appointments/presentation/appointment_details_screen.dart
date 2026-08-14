import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../calendar/calendar_event_data.dart';
import '../../calendar/calendar_service.dart';
import '../../calendar/calendar_write_result.dart';
import '../../doctors/presentation/doctor_detail_controller.dart';
import '../../payments/domain/appointment_payment_status.dart';
import '../domain/appointment_summary.dart';
import '../domain/cancellation_preview.dart';
import 'appointment_details_controller.dart';

String _formatDateTime(DateTime utc) {
  final local = utc.toLocal();
  final hour12 = local.hour % 12 == 0 ? 12 : local.hour % 12;
  final minute = local.minute.toString().padLeft(2, '0');
  final period = local.hour < 12 ? 'AM' : 'PM';
  return '${local.year}-${local.month.toString().padLeft(2, '0')}-${local.day.toString().padLeft(2, '0')} · $hour12:$minute $period';
}

String _statusLabel(AppointmentPaymentStatus status) => switch (status) {
      AppointmentPaymentStatus.paymentPending => 'Payment pending',
      AppointmentPaymentStatus.scheduled => 'Scheduled',
      AppointmentPaymentStatus.completed => 'Completed',
      AppointmentPaymentStatus.noShow => 'No-show',
      AppointmentPaymentStatus.cancelledByPatient => 'Cancelled by you',
      AppointmentPaymentStatus.cancelledByDoctor => 'Cancelled by doctor',
      AppointmentPaymentStatus.cancelledByAdmin => 'Cancelled',
      AppointmentPaymentStatus.refundPending => 'Refund pending',
      AppointmentPaymentStatus.refunded => 'Refunded',
      AppointmentPaymentStatus.paymentFailed => 'Payment failed',
      AppointmentPaymentStatus.expired => 'Expired',
    };

const _cancellableStatuses = {AppointmentPaymentStatus.scheduled};

/// Appointment details, cancellation, "Add to Calendar", and a chat entry
/// point (#26) — features/chat itself doesn't exist yet (Milestone 7), so
/// this is a present-but-not-yet-wired affordance rather than a fake
/// destination, same principle applied elsewhere in this app (e.g. no
/// fabricated doctor ratings).
class AppointmentDetailsScreen extends ConsumerStatefulWidget {
  const AppointmentDetailsScreen({super.key, required this.appointment});

  final AppointmentSummary appointment;

  @override
  ConsumerState<AppointmentDetailsScreen> createState() => _AppointmentDetailsScreenState();
}

class _AppointmentDetailsScreenState extends ConsumerState<AppointmentDetailsScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(appointmentDetailsControllerProvider(widget.appointment.id).notifier).initialize(widget.appointment);
    });
  }

  Future<void> _addToCalendar(AppointmentSummary appointment, String? location) async {
    final result = await ref.read(calendarServiceProvider).addEvent(CalendarEventData(
          title: 'Appointment with ${appointment.doctorFullName}',
          startUtc: appointment.slotStartUtc,
          endUtc: appointment.slotEndUtc,
          location: location,
          description: 'Asnan appointment with ${appointment.doctorFullName}.',
        ));

    if (!mounted) return;

    final message = switch (result.status) {
      CalendarWriteStatus.success => 'Added to your calendar.',
      CalendarWriteStatus.permissionDenied => 'Calendar permission was not granted.',
      CalendarWriteStatus.noWritableCalendar => 'No writable calendar was found on this device.',
      CalendarWriteStatus.failure => 'Could not add to your calendar. Please try again.',
    };
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  Future<void> _showCancellationDialog(CancellationPreview preview) async {
    final controller = ref.read(appointmentDetailsControllerProvider(widget.appointment.id).notifier);

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Cancel appointment?'),
        content: Text(
          preview.isAllowed
              ? "You'll receive a refund of ${preview.currency} ${preview.refundAmount.toStringAsFixed(2)} (${preview.refundPercentage}% of the consultation fee)."
              : 'This appointment is too close to its scheduled time to cancel.',
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Keep appointment')),
          if (preview.isAllowed)
            FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Cancel appointment')),
        ],
      ),
    );

    controller.dismissCancellationPreview();
    if (confirmed == true) {
      await controller.confirmCancellation(null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(appointmentDetailsControllerProvider(widget.appointment.id));
    final controller = ref.read(appointmentDetailsControllerProvider(widget.appointment.id).notifier);
    final appointment = state.appointment ?? widget.appointment;
    final asyncDoctor = ref.watch(doctorDetailProvider(appointment.doctorProfileId));

    ref.listen(appointmentDetailsControllerProvider(widget.appointment.id), (previous, next) {
      if (next.preview != null && previous?.preview == null) {
        _showCancellationDialog(next.preview!);
      }
      if (next.cancelled && previous?.cancelled != true) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Appointment cancelled.')));
      }
    });

    final textTheme = Theme.of(context).textTheme;
    final canCancel = _cancellableStatuses.contains(appointment.status);
    final canAddToCalendarOrChat = appointment.status == AppointmentPaymentStatus.scheduled;

    return Scaffold(
      appBar: AppBar(title: const Text('Appointment Details')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(appointment.doctorFullName, style: textTheme.titleLarge),
          const SizedBox(height: 4),
          Text(_statusLabel(appointment.status), style: textTheme.bodyMedium),
          const Divider(height: 32),
          _DetailRow(icon: Icons.event, label: _formatDateTime(appointment.slotStartUtc)),
          asyncDoctor.when(
            loading: () => const SizedBox.shrink(),
            error: (_, _) => const SizedBox.shrink(),
            data: (doctor) => (doctor.clinicAddress?.isNotEmpty ?? false)
                ? _DetailRow(icon: Icons.location_on, label: doctor.clinicAddress!)
                : const SizedBox.shrink(),
          ),
          _DetailRow(icon: Icons.payments, label: '${appointment.currency} ${appointment.consultationFee.toStringAsFixed(2)}'),
          if (state.previewFailure != null) ...[
            const SizedBox(height: 16),
            _ErrorBanner(message: state.previewFailure!.message),
          ],
          if (state.cancelFailure != null) ...[
            const SizedBox(height: 16),
            _ErrorBanner(message: state.cancelFailure!.message),
          ],
          const SizedBox(height: 24),
          if (canAddToCalendarOrChat) ...[
            FilledButton.icon(
              icon: const Icon(Icons.calendar_month),
              label: const Text('Add to Calendar'),
              onPressed: asyncDoctor.maybeWhen(
                data: (doctor) => () => _addToCalendar(appointment, doctor.clinicAddress),
                orElse: () => () => _addToCalendar(appointment, null),
              ),
            ),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              icon: const Icon(Icons.chat_bubble_outline),
              label: const Text('Message Doctor'),
              onPressed: appointment.chatConversationId == null
                  ? null
                  : () => context.pushNamed(
                        AppRoutes.chat,
                        pathParameters: {'conversationId': appointment.chatConversationId!},
                        extra: appointment.doctorFullName,
                      ),
            ),
            const SizedBox(height: 12),
          ],
          if (canCancel)
            OutlinedButton(
              onPressed: state.isLoadingPreview || state.isCancelling ? null : controller.loadCancellationPreview,
              child: state.isLoadingPreview
                  ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Cancel Appointment'),
            ),
        ],
      ),
    );
  }
}

class _DetailRow extends StatelessWidget {
  const _DetailRow({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Row(
        children: [
          Icon(icon, size: 20, color: Theme.of(context).colorScheme.onSurfaceVariant),
          const SizedBox(width: 12),
          Expanded(child: Text(label)),
        ],
      ),
    );
  }
}

class _ErrorBanner extends StatelessWidget {
  const _ErrorBanner({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.errorContainer,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(message, style: TextStyle(color: Theme.of(context).colorScheme.onErrorContainer)),
    );
  }
}
