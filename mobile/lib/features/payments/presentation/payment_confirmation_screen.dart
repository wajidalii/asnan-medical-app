import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../booking/presentation/booking_controller.dart';
import '../../calendar/calendar_event_data.dart';
import '../../calendar/calendar_service.dart';
import '../../calendar/calendar_write_result.dart';
import '../../doctors/presentation/doctor_detail_controller.dart';
import '../domain/appointment_payment_status.dart';
import 'payment_controller.dart';
import 'payment_state.dart';

String _formatDateTime(DateTime utc) {
  const weekdays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
  const months = [
    'January', 'February', 'March', 'April', 'May', 'June',
    'July', 'August', 'September', 'October', 'November', 'December',
  ];
  final local = utc.toLocal();
  final hour12 = local.hour % 12 == 0 ? 12 : local.hour % 12;
  final minute = local.minute.toString().padLeft(2, '0');
  final period = local.hour < 12 ? 'AM' : 'PM';
  return '${weekdays[local.weekday - 1]}, ${months[local.month - 1]} ${local.day} at $hour12:$minute $period';
}

const _terminalFailureStatuses = {
  AppointmentPaymentStatus.paymentFailed,
  AppointmentPaymentStatus.expired,
};

/// Provider handoff + confirmation (#22). Never shows "Scheduled" from the
/// mock-confirm/webhook-relay calls' own success — only once PaymentController's
/// poll observes the appointment's status has actually left PaymentPending
/// server-side, per ARCHITECTURE.md §8's "never trust client-reported success".
class PaymentConfirmationScreen extends ConsumerWidget {
  const PaymentConfirmationScreen({super.key, required this.doctorId});

  final String doctorId;

  void _restartBooking(BuildContext context, WidgetRef ref) {
    ref.read(bookingControllerProvider(doctorId).notifier).abandonHold();
    context.goNamed(AppRoutes.booking, pathParameters: {'id': doctorId});
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(paymentControllerProvider(doctorId));
    final controller = ref.read(paymentControllerProvider(doctorId).notifier);
    final checkout = state.checkout;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Payment'),
        automaticallyImplyLeading: false,
      ),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: checkout == null
              ? const Text('No checkout in progress.', textAlign: TextAlign.center)
              : switch (checkout.status) {
                  AppointmentPaymentStatus.scheduled => _Success(doctorId: doctorId),
                  final status when _terminalFailureStatuses.contains(status) => _Failure(
                      message: 'Your payment could not be completed.',
                      onRetry: () => _restartBooking(context, ref),
                    ),
                  _ => _PendingFlow(doctorId: doctorId, state: state, controller: controller),
                },
        ),
      ),
    );
  }
}

class _PendingFlow extends StatelessWidget {
  const _PendingFlow({required this.doctorId, required this.state, required this.controller});

  final String doctorId;
  final PaymentState state;
  final PaymentController controller;

  @override
  Widget build(BuildContext context) {
    if (state.isPolling) {
      return const Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          CircularProgressIndicator(),
          SizedBox(height: 16),
          Text('Confirming your appointment...', textAlign: TextAlign.center),
        ],
      );
    }

    if (state.pollTimedOut) {
      return Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text('Still waiting for confirmation. This can take a moment.', textAlign: TextAlign.center),
          const SizedBox(height: 16),
          FilledButton(onPressed: controller.retryPolling, child: const Text('Check again')),
        ],
      );
    }

    return _MockProviderHandoff(state: state, controller: controller);
  }
}

/// Stands in for a real provider's hosted checkout UI — only the mock
/// provider exists today (#19); a real provider's SDK/redirect replaces this
/// widget behind the same PaymentController once one is integrated (#60).
class _MockProviderHandoff extends StatelessWidget {
  const _MockProviderHandoff({required this.state, required this.controller});

  final PaymentState state;
  final PaymentController controller;

  @override
  Widget build(BuildContext context) {
    final busy = state.isSubmittingMockOutcome;

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('Mock Payment Provider', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 8),
        const Text(
          'No real payment provider is configured yet — simulate the outcome below.',
          textAlign: TextAlign.center,
        ),
        if (state.mockOutcomeFailure != null) ...[
          const SizedBox(height: 16),
          Text(
            state.mockOutcomeFailure!.message,
            style: TextStyle(color: Theme.of(context).colorScheme.error),
            textAlign: TextAlign.center,
          ),
        ],
        const SizedBox(height: 24),
        FilledButton(
          onPressed: busy ? null : () => controller.submitMockOutcome(succeeded: true),
          child: const Text('Simulate Successful Payment'),
        ),
        const SizedBox(height: 12),
        OutlinedButton(
          onPressed: busy
              ? null
              : () => controller.submitMockOutcome(succeeded: false, failureReason: 'Card declined'),
          child: const Text('Simulate Failed Payment'),
        ),
        if (busy) ...[
          const SizedBox(height: 16),
          const CircularProgressIndicator(),
        ],
      ],
    );
  }
}

class _Success extends ConsumerWidget {
  const _Success({required this.doctorId});

  final String doctorId;

  Future<void> _addToCalendar(BuildContext context, WidgetRef ref, String doctorName, DateTime startUtc, DateTime endUtc) async {
    final result = await ref.read(calendarServiceProvider).addEvent(CalendarEventData(
          title: 'Appointment with $doctorName',
          startUtc: startUtc,
          endUtc: endUtc,
          description: 'Asnan appointment with $doctorName.',
        ));

    if (!context.mounted) return;

    final message = switch (result.status) {
      CalendarWriteStatus.success => 'Added to your calendar.',
      CalendarWriteStatus.permissionDenied => 'Calendar permission was not granted.',
      CalendarWriteStatus.noWritableCalendar => 'No writable calendar was found on this device.',
      CalendarWriteStatus.failure => 'Could not add to your calendar. Please try again.',
    };
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final hold = ref.watch(bookingControllerProvider(doctorId)).activeHold;
    final asyncDoctor = ref.watch(doctorDetailProvider(doctorId));
    final doctorName = asyncDoctor.maybeWhen(data: (doctor) => doctor.fullName, orElse: () => null);

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 64,
          height: 64,
          alignment: Alignment.center,
          decoration: BoxDecoration(border: Border.all(color: theme.colorScheme.outline)),
          child: Icon(Icons.check, size: 28, color: theme.textTheme.bodyMedium?.color?.withValues(alpha: 0.7)),
        ),
        const SizedBox(height: 20),
        Text('Appointment confirmed', style: theme.textTheme.headlineMedium),
        const SizedBox(height: 8),
        Text(
          doctorName != null && hold != null
              ? "You're booked with $doctorName on ${_formatDateTime(hold.slotStartUtc)}."
              : 'Your payment was successful and the appointment is scheduled.',
          style: theme.textTheme.bodySmall?.copyWith(height: 1.6, color: theme.textTheme.bodySmall!.color!.withValues(alpha: 0.6)),
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 26),
        SizedBox(
          width: double.infinity,
          child: FilledButton(
            onPressed: () => context.goNamed(AppRoutes.home),
            child: const Text('Done'),
          ),
        ),
        if (doctorName != null && hold != null) ...[
          const SizedBox(height: 10),
          SizedBox(
            width: double.infinity,
            child: OutlinedButton.icon(
              onPressed: () => _addToCalendar(context, ref, doctorName, hold.slotStartUtc, hold.slotEndUtc),
              icon: const Icon(Icons.calendar_month, size: 16),
              label: const Text('Add to calendar'),
            ),
          ),
        ],
      ],
    );
  }
}

class _Failure extends StatelessWidget {
  const _Failure({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 64,
          height: 64,
          alignment: Alignment.center,
          decoration: BoxDecoration(border: Border.all(color: theme.colorScheme.error)),
          child: Icon(Icons.close, size: 28, color: theme.colorScheme.error),
        ),
        const SizedBox(height: 20),
        Text('Payment failed', style: theme.textTheme.headlineMedium),
        const SizedBox(height: 8),
        Text(
          message,
          style: theme.textTheme.bodySmall?.copyWith(height: 1.6, color: theme.textTheme.bodySmall!.color!.withValues(alpha: 0.6)),
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 26),
        SizedBox(width: double.infinity, child: FilledButton(onPressed: onRetry, child: const Text('Try Again'))),
      ],
    );
  }
}
