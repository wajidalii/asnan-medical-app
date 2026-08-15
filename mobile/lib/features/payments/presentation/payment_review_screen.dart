import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../../core/theme/design_tokens.dart';
import '../../../core/widgets/error_banner.dart';
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

    final theme = Theme.of(context);
    final divider = theme.colorScheme.outlineVariant;

    return Scaffold(
      appBar: AppBar(title: const Text('Review & pay')),
      body: hold == null
          ? const Center(
              child: Padding(
                padding: EdgeInsets.all(24),
                child: Text('Your hold is no longer active. Please select a slot again.', textAlign: TextAlign.center),
              ),
            )
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
                      OutlinedButton(
                        onPressed: () => ref.invalidate(doctorDetailProvider(doctorId)),
                        child: const Text('Retry'),
                      ),
                    ],
                  ),
                ),
              ),
              data: (doctor) {
                final textTheme = theme.textTheme;
                return Column(
                  children: [
                    Padding(
                      padding: const EdgeInsets.fromLTRB(20, 4, 20, 18),
                      child: Text(
                        'STEP 3 OF 3',
                        style: textTheme.labelSmall?.copyWith(
                          color: theme.brightness == Brightness.dark ? AppColors.accentOnDark : AppColors.accent700,
                          letterSpacing: 1.2,
                        ),
                      ),
                    ),
                    Expanded(
                      child: Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 20),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            if (paymentState.checkoutFailure != null) ...[
                              ErrorBanner(message: paymentState.checkoutFailure!.message),
                              const SizedBox(height: 16),
                            ],
                            Container(
                              padding: const EdgeInsets.all(18),
                              decoration: BoxDecoration(border: Border.all(color: divider)),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Container(
                                    padding: const EdgeInsets.only(bottom: 16),
                                    decoration: BoxDecoration(border: Border(bottom: BorderSide(color: divider))),
                                    child: Row(
                                      children: [
                                        Container(
                                          width: 48,
                                          height: 48,
                                          decoration: BoxDecoration(border: Border.all(color: divider), color: theme.colorScheme.surfaceContainerHighest),
                                          child: Icon(Icons.person_outline, color: textTheme.bodyMedium?.color?.withValues(alpha: 0.4)),
                                        ),
                                        const SizedBox(width: 12),
                                        Expanded(
                                          child: Column(
                                            crossAxisAlignment: CrossAxisAlignment.start,
                                            children: [
                                              Text(doctor.fullName, style: textTheme.titleMedium),
                                              if (doctor.specialties.isNotEmpty)
                                                Text(
                                                  doctor.specialties.map((s) => s.name).join(', '),
                                                  style: textTheme.bodySmall?.copyWith(color: textTheme.bodySmall!.color!.withValues(alpha: 0.6)),
                                                ),
                                            ],
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                  const SizedBox(height: 14),
                                  _ReviewRow(label: 'Date & time', value: '${_formatDate(hold.slotStartUtc)} · ${_formatTime(hold.slotStartUtc)}'),
                                  _ReviewRow(label: 'Consultation fee', value: '${doctor.currency} ${doctor.consultationFee.toStringAsFixed(2)}'),
                                  if (doctor.clinicAddress != null && doctor.clinicAddress!.isNotEmpty)
                                    _ReviewRow(label: 'Clinic', value: doctor.clinicAddress!),
                                  Container(
                                    margin: const EdgeInsets.only(top: 6),
                                    padding: const EdgeInsets.only(top: 12),
                                    decoration: BoxDecoration(border: Border(top: BorderSide(color: divider))),
                                    child: Row(
                                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                      children: [
                                        Text('Total', style: textTheme.titleSmall),
                                        Text('${doctor.currency} ${doctor.consultationFee.toStringAsFixed(2)}', style: textTheme.titleLarge),
                                      ],
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                    Padding(
                      padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
                      child: FilledButton(
                        onPressed: paymentState.isCheckingOut
                            ? null
                            : () => ref.read(paymentControllerProvider(doctorId).notifier).startCheckout(hold.holdToken),
                        child: paymentState.isCheckingOut
                            ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                            : Text('Pay ${doctor.currency} ${doctor.consultationFee.toStringAsFixed(0)}'),
                      ),
                    ),
                  ],
                );
              },
            ),
    );
  }
}

class _ReviewRow extends StatelessWidget {
  const _ReviewRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: textTheme.bodySmall?.copyWith(color: textTheme.bodySmall!.color!.withValues(alpha: 0.75))),
          Text(value, style: textTheme.bodySmall),
        ],
      ),
    );
  }
}
