import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';
import '../../../core/theme/design_tokens.dart';
import '../../../core/widgets/error_banner.dart';
import 'booking_controller.dart';
import 'booking_state.dart';

const _weekdayAbbrev = ['MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT', 'SUN'];
const _weekdayFull = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
const _monthFull = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

String _formatTime(DateTime utcTime) {
  final local = utcTime.toLocal();
  final hour12 = local.hour % 12 == 0 ? 12 : local.hour % 12;
  final minute = local.minute.toString().padLeft(2, '0');
  final period = local.hour < 12 ? 'AM' : 'PM';
  return '$hour12:$minute $period';
}

String _formatFullDate(DateTime date) => '${_weekdayFull[date.weekday - 1]}, ${_monthFull[date.month - 1]} ${date.day}';

String _formatShortDate(DateTime utc) {
  final local = utc.toLocal();
  return '${_weekdayFull[local.weekday - 1].substring(0, 3)}, ${_monthFull[local.month - 1].substring(0, 3)} ${local.day}';
}

/// Exposed so widget tests can locate/tap a specific date-strip cell without
/// depending on its rendered text (the cell now stacks weekday-abbrev and
/// day-of-month as separate Text widgets per the design, so no single
/// findable string like "Mon 12" exists anymore).
@visibleForTesting
Key dateCellKey(DateTime date) => ValueKey('date-cell-${date.year}-${date.month}-${date.day}');

String _formatCountdown(int totalSeconds) {
  final clamped = totalSeconds < 0 ? 0 : totalSeconds;
  final m = (clamped ~/ 60).toString().padLeft(2, '0');
  final s = (clamped % 60).toString().padLeft(2, '0');
  return '$m:$s';
}

class BookingScreen extends ConsumerStatefulWidget {
  const BookingScreen({super.key, required this.doctorId});

  final String doctorId;

  @override
  ConsumerState<BookingScreen> createState() => _BookingScreenState();
}

class _BookingScreenState extends ConsumerState<BookingScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(bookingControllerProvider(widget.doctorId).notifier).loadDateStrip();
    });
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(bookingControllerProvider(widget.doctorId));
    final controller = ref.read(bookingControllerProvider(widget.doctorId).notifier);
    final onHold = state.activeHold != null;

    return Scaffold(
      appBar: AppBar(title: Text(onHold ? 'Confirm your slot' : 'Choose a time')),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 4, 20, 14),
            child: Text(
              onHold ? 'STEP 2 OF 3' : 'STEP 1 OF 3',
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                    color: Theme.of(context).brightness == Brightness.dark ? AppColors.accentOnDark : AppColors.accent700,
                    letterSpacing: 1.2,
                  ),
            ),
          ),
          Expanded(
            child: onHold ? _HoldConfirmation(state: state, controller: controller) : _SlotSelection(state: state, controller: controller),
          ),
        ],
      ),
    );
  }
}

class _SlotSelection extends StatelessWidget {
  const _SlotSelection({required this.state, required this.controller});

  final BookingState state;
  final BookingController controller;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (state.holdFailure != null)
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 0, 20, 12),
            child: ErrorBanner(message: state.holdFailure!.message),
          ),
        if (state.holdExpired)
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 0, 20, 12),
            child: _DismissibleBanner(
              message: 'Your hold has expired. Please pick another slot.',
              onDismiss: controller.dismissExpiredNotice,
            ),
          ),
        if (state.isLoadingDateStrip)
          const Expanded(child: Center(child: CircularProgressIndicator()))
        else if (state.dateStripFailure != null)
          Expanded(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Align(
                alignment: Alignment.topCenter,
                child: _InlineErrorRow(message: state.dateStripFailure!.message, onRetry: controller.retryDateStrip),
              ),
            ),
          )
        else ...[
          _DateStrip(state: state, controller: controller),
          if (state.selectedDate != null)
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 0, 20, 8),
              child: Text(
                _formatFullDate(state.selectedDate!),
                style: Theme.of(context).textTheme.bodySmall?.copyWith(color: Theme.of(context).textTheme.bodySmall!.color!.withValues(alpha: 0.6)),
              ),
            ),
          Expanded(child: _SlotList(state: state, controller: controller)),
        ],
      ],
    );
  }
}

class _DateStrip extends StatelessWidget {
  const _DateStrip({required this.state, required this.controller});

  final BookingState state;
  final BookingController controller;

  @override
  Widget build(BuildContext context) {
    final dates = state.dateAvailability.keys.toList();
    final theme = Theme.of(context);
    final divider = theme.colorScheme.outlineVariant;

    return SizedBox(
      height: 68,
      child: ListView.separated(
        padding: const EdgeInsets.symmetric(horizontal: 20),
        scrollDirection: Axis.horizontal,
        itemCount: dates.length,
        separatorBuilder: (_, _) => const SizedBox(width: 8),
        itemBuilder: (context, index) {
          final date = dates[index];
          final hasAvailability = state.dateAvailability[date] ?? false;
          final selected = state.selectedDate != null &&
              state.selectedDate!.year == date.year &&
              state.selectedDate!.month == date.month &&
              state.selectedDate!.day == date.day;

          final dimmed = !hasAvailability && !selected;
          final textColor = selected
              ? theme.colorScheme.onPrimary
              : dimmed
                  ? theme.textTheme.bodyMedium?.color?.withValues(alpha: 0.3)
                  : theme.textTheme.bodyMedium?.color;

          return InkWell(
            key: dateCellKey(date),
            onTap: hasAvailability ? () => controller.selectDate(date) : null,
            child: Container(
              width: 64,
              padding: const EdgeInsets.symmetric(vertical: 8),
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: selected ? AppColors.accent : null,
                border: selected ? null : Border.all(color: dimmed ? divider.withValues(alpha: 0.5) : divider),
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(_weekdayAbbrev[date.weekday - 1], style: theme.textTheme.labelSmall?.copyWith(color: textColor)),
                  Text('${date.day}', style: theme.textTheme.titleMedium?.copyWith(color: textColor)),
                  if (hasAvailability && !selected) ...[
                    const SizedBox(height: 3),
                    Container(width: 4, height: 4, decoration: const BoxDecoration(shape: BoxShape.circle, color: AppColors.accent)),
                  ],
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}

class _SlotList extends StatelessWidget {
  const _SlotList({required this.state, required this.controller});

  final BookingState state;
  final BookingController controller;

  @override
  Widget build(BuildContext context) {
    if (state.isLoadingSlots) {
      return const Center(child: CircularProgressIndicator());
    }

    if (state.slotsFailure != null) {
      return Padding(
        padding: const EdgeInsets.symmetric(horizontal: 20),
        child: Align(
          alignment: Alignment.topCenter,
          child: _InlineErrorRow(message: state.slotsFailure!.message, onRetry: controller.retrySlots),
        ),
      );
    }

    if (state.slots.isEmpty) {
      final theme = Theme.of(context);
      return Center(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 44),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.calendar_month_outlined, size: 30, color: theme.colorScheme.outline),
              const SizedBox(height: 12),
              Text('No slots available this day', style: theme.textTheme.titleMedium, textAlign: TextAlign.center),
              const SizedBox(height: 6),
              Text(
                'Try another day with a dot.',
                style: theme.textTheme.bodySmall?.copyWith(color: theme.textTheme.bodySmall!.color!.withValues(alpha: 0.6)),
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      );
    }

    return GridView.builder(
      padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 3,
        mainAxisSpacing: 10,
        crossAxisSpacing: 10,
        childAspectRatio: 2.2,
      ),
      itemCount: state.slots.length,
      itemBuilder: (context, index) {
        final slot = state.slots[index];
        return OutlinedButton(
          onPressed: state.isCreatingHold ? null : () => controller.selectSlot(slot),
          style: const ButtonStyle(alignment: Alignment.center, padding: WidgetStatePropertyAll(EdgeInsets.zero)),
          child: Text(_formatTime(slot.startUtc)),
        );
      },
    );
  }
}

class _HoldConfirmation extends StatelessWidget {
  const _HoldConfirmation({required this.state, required this.controller});

  final BookingState state;
  final BookingController controller;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final divider = theme.colorScheme.outlineVariant;
    // Non-null by construction: the parent only builds this widget while
    // state.activeHold != null (expiry clears it and flips back to
    // _SlotSelection, which shows its own recovery banner instead).
    final hold = state.activeHold!;

    return Column(
      children: [
        Expanded(
          child: Center(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                    decoration: BoxDecoration(border: Border.all(color: const Color(0xFFB8863C))),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(Icons.schedule, size: 13, color: Color(0xFFB8863C)),
                        const SizedBox(width: 7),
                        Text('SLOT RESERVED', style: textTheme.labelSmall?.copyWith(color: const Color(0xFFB8863C), letterSpacing: 0.6)),
                      ],
                    ),
                  ),
                  const SizedBox(height: 18),
                  Text(
                    _formatCountdown(state.remainingSeconds),
                    style: textTheme.displaySmall?.copyWith(letterSpacing: -0.5),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    'This time is held for you. Complete payment before it expires.',
                    style: textTheme.bodySmall?.copyWith(color: textTheme.bodySmall!.color!.withValues(alpha: 0.6)),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 24),
                  Container(
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(border: Border.all(color: divider)),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Container(
                          width: 44,
                          height: 44,
                          decoration: BoxDecoration(border: Border.all(color: divider), color: theme.colorScheme.surfaceContainerHighest),
                          child: Icon(Icons.person_outline, color: textTheme.bodyMedium?.color?.withValues(alpha: 0.4)),
                        ),
                        const SizedBox(width: 12),
                        Text('${_formatShortDate(hold.slotStartUtc)} · ${_formatTime(hold.slotStartUtc)}', style: textTheme.bodySmall),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
          child: Column(
            children: [
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: () => context.pushNamed(AppRoutes.paymentReview, pathParameters: {'id': state.doctorId}),
                  child: const Text('Continue to payment'),
                ),
              ),
              const SizedBox(height: 10),
              SizedBox(width: double.infinity, child: OutlinedButton(onPressed: controller.abandonHold, child: const Text('Cancel hold'))),
            ],
          ),
        ),
      ],
    );
  }
}


/// The date-strip/slot-list failure look: an inline accent-bordered row with
/// an underlined inline "Retry" action, matching the design system's dense
/// error treatment for a strip/grid that's otherwise empty.
class _InlineErrorRow extends StatelessWidget {
  const _InlineErrorRow({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final error = Theme.of(context).colorScheme.error;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(border: Border.all(color: error)),
      child: Row(
        children: [
          Icon(Icons.error_outline, size: 16, color: error),
          const SizedBox(width: 10),
          Expanded(child: Text(message, style: Theme.of(context).textTheme.bodySmall?.copyWith(color: error))),
          TextButton(
            onPressed: onRetry,
            style: TextButton.styleFrom(foregroundColor: error, textStyle: const TextStyle(decoration: TextDecoration.underline)),
            child: const Text('Retry'),
          ),
        ],
      ),
    );
  }
}

class _DismissibleBanner extends StatelessWidget {
  const _DismissibleBanner({required this.message, this.onDismiss});

  final String message;
  final VoidCallback? onDismiss;

  @override
  Widget build(BuildContext context) {
    final error = Theme.of(context).colorScheme.error;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(border: Border.all(color: error)),
      child: Row(
        children: [
          Expanded(child: Text(message, style: Theme.of(context).textTheme.bodySmall?.copyWith(color: error))),
          if (onDismiss != null) IconButton(icon: Icon(Icons.close, color: error), onPressed: onDismiss, iconSize: 18),
        ],
      ),
    );
  }
}
