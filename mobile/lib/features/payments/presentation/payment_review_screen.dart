import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../booking/presentation/booking_controller.dart';
import '../../doctors/presentation/doctor_detail_controller.dart';
import 'payment_controller.dart';

String _formatTime(DateTime utcTime) {
  final local = utcTime.toLocal();
  final hour12 = local.hour % 12 == 0 ? 12 : local.hour % 12;
  final minute = local.minute.toString().padLeft(2, '0');
  final period = local.hour < 12 ? 'AM' : 'PM';
  return '$hour12:$minute $period';
}

String _formatDate(DateTime utcTime) {
  final local = utcTime.toLocal();
  return '${local.year}-${local.month.toString().padLeft(2, '0')}-${local.day.toString().padLeft(2, '0')}';
}

/// Booking review — shows doctor/slot/fee before payment (#22). Reads the
/// active hold from BookingController (same doctorId family instance the
/// booking screen already populated, still alive since navigation here uses
/// push, not go) rather than duplicating hold state in this feature.
class PaymentReviewScreen extends ConsumerWidget {
  const PaymentReviewScreen({super.key, required this.doctorId});

  final String doctorId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final bookingState = ref.watch(bookingControllerProvider(doctorId));
    final asyncDoctor = ref.watch(doctorDetailProvider(doctorId));
    final paymentState = ref.watch(paymentControllerProvider(doctorId));

    ref.listen(bookingControllerProvider(doctorId), (previous, next) {
      if (next.holdExpired && context.canPop()) {
        context.pop();
      }
    });

    ref.listen(paymentControllerProvider(doctorId), (previous, next) {
      final justCheckedOut = previous?.checkout == null && next.checkout != null;
      if (justCheckedOut) {
        context.pushNamed(AppRoutes.paymentConfirmation, pathParameters: {'id': doctorId});
      }
    });

    final hold = bookingState.activeHold;

    return Scaffold(
      appBar: AppBar(title: const Text('Review & Pay')),
      body: hold == null
          ? const Center(child: Text('Your hold is no longer active. Please select a slot again.'))
          : asyncDoctor.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Text('Could not load doctor details.', textAlign: TextAlign.center),
                      const SizedBox(height: 12),
                      FilledButton(
                        onPressed: () => ref.invalidate(doctorDetailProvider(doctorId)),
                        child: const Text('Retry'),
                      ),
                    ],
                  ),
                ),
              ),
              data: (doctor) {
                final textTheme = Theme.of(context).textTheme;
                return Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      if (paymentState.checkoutFailure != null)
                        Padding(
                          padding: const EdgeInsets.only(bottom: 16),
                          child: Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: Theme.of(context).colorScheme.errorContainer,
                              borderRadius: BorderRadius.circular(8),
                            ),
                            child: Text(
                              paymentState.checkoutFailure!.message,
                              style: TextStyle(color: Theme.of(context).colorScheme.onErrorContainer),
                            ),
                          ),
                        ),
                      Card(
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(doctor.fullName, style: textTheme.titleLarge),
                              const SizedBox(height: 12),
                              _ReviewRow(icon: Icons.calendar_today, label: _formatDate(hold.slotStartUtc)),
                              _ReviewRow(
                                icon: Icons.access_time,
                                label: '${_formatTime(hold.slotStartUtc)} - ${_formatTime(hold.slotEndUtc)}',
                              ),
                              if (doctor.clinicAddress != null && doctor.clinicAddress!.isNotEmpty)
                                _ReviewRow(icon: Icons.location_on, label: doctor.clinicAddress!),
                              const Divider(height: 32),
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  Text('Consultation fee', style: textTheme.bodyLarge),
                                  Text(
                                    '${doctor.currency} ${doctor.consultationFee.toStringAsFixed(0)}',
                                    style: textTheme.titleMedium,
                                  ),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ),
                      const Spacer(),
                      FilledButton(
                        onPressed: paymentState.isCheckingOut
                            ? null
                            : () => ref.read(paymentControllerProvider(doctorId).notifier).startCheckout(hold.holdToken),
                        child: paymentState.isCheckingOut
                            ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                            : Text('Pay ${doctor.currency} ${doctor.consultationFee.toStringAsFixed(0)}'),
                      ),
                    ],
                  ),
                );
              },
            ),
    );
  }
}

class _ReviewRow extends StatelessWidget {
  const _ReviewRow({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 6),
      child: Row(
        children: [
          Icon(icon, size: 18, color: Theme.of(context).colorScheme.onSurfaceVariant),
          const SizedBox(width: 8),
          Expanded(child: Text(label)),
        ],
      ),
    );
  }
}
