import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../booking/presentation/booking_controller.dart';
import '../domain/appointment_payment_status.dart';
import 'payment_controller.dart';
import 'payment_state.dart';

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
                  AppointmentPaymentStatus.scheduled => const _Success(),
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

class _Success extends StatelessWidget {
  const _Success();

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.check_circle, size: 64, color: Theme.of(context).colorScheme.primary),
        const SizedBox(height: 16),
        Text('Appointment Confirmed', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 8),
        const Text('Your payment was successful and the appointment is scheduled.', textAlign: TextAlign.center),
        const SizedBox(height: 24),
        FilledButton(
          onPressed: () => context.goNamed(AppRoutes.home),
          child: const Text('Done'),
        ),
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
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.error_outline, size: 64, color: Theme.of(context).colorScheme.error),
        const SizedBox(height: 16),
        Text(message, textAlign: TextAlign.center),
        const SizedBox(height: 24),
        FilledButton(onPressed: onRetry, child: const Text('Try Again')),
      ],
    );
  }
}
