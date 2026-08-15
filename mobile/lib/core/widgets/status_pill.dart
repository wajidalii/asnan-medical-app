import 'package:flutter/material.dart';

import '../theme/design_tokens.dart';
import '../../features/payments/domain/appointment_payment_status.dart';

/// A small status tag matching the design system's `.tag-accent` /
/// `.tag-neutral` language: the single accent marks a state that needs the
/// patient's attention (scheduled, pending, failed); everything settled
/// (completed, cancelled, refunded) reads as neutral gray — the mono-accent
/// system deliberately has no second hue for "done" vs. "attention".
class StatusPill extends StatelessWidget {
  const StatusPill({super.key, required this.label, required this.emphasis});

  StatusPill.forStatus(AppointmentPaymentStatus status, {super.key})
      : label = _labelFor(status),
        emphasis = _emphasisFor(status);

  final String label;
  final bool emphasis;

  static String _labelFor(AppointmentPaymentStatus status) => switch (status) {
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

  static bool _emphasisFor(AppointmentPaymentStatus status) => switch (status) {
        AppointmentPaymentStatus.paymentPending ||
        AppointmentPaymentStatus.scheduled ||
        AppointmentPaymentStatus.refundPending ||
        AppointmentPaymentStatus.paymentFailed =>
          true,
        _ => false,
      };

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final Color bg;
    final Color fg;
    if (emphasis) {
      bg = isDark ? AppColors.accent900 : AppColors.accent100;
      fg = isDark ? AppColors.accent300 : AppColors.accentTextOnTint;
    } else {
      bg = isDark ? AppColors.neutral800 : AppColors.neutral100;
      fg = isDark ? AppColors.neutral300 : AppColors.neutralTextOnTint;
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
      decoration: BoxDecoration(color: bg),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelSmall?.copyWith(color: fg, letterSpacing: 0.02),
      ),
    );
  }
}
