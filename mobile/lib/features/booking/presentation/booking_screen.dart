import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'booking_controller.dart';
import 'booking_state.dart';

const _weekdayAbbrev = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

String _formatDateLabel(DateTime date) => '${_weekdayAbbrev[date.weekday - 1]} ${date.day}';

String _formatTime(DateTime utcTime) {
  final local = utcTime.toLocal();
  final hour12 = local.hour % 12 == 0 ? 12 : local.hour % 12;
  final minute = local.minute.toString().padLeft(2, '0');
  final period = local.hour < 12 ? 'AM' : 'PM';
  return '$hour12:$minute $period';
}

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

    return Scaffold(
      appBar: AppBar(title: const Text('Book Appointment')),
      body: state.activeHold != null
          ? _HoldConfirmation(state: state, controller: controller)
          : _SlotSelection(state: state, controller: controller),
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
          _Banner(
            message: state.holdFailure!.message,
            color: Theme.of(context).colorScheme.errorContainer,
            onColor: Theme.of(context).colorScheme.onErrorContainer,
          ),
        if (state.holdExpired)
          _Banner(
            message: 'Your hold has expired. Please pick another slot.',
            color: Theme.of(context).colorScheme.errorContainer,
            onColor: Theme.of(context).colorScheme.onErrorContainer,
            onDismiss: controller.dismissExpiredNotice,
          ),
        if (state.isLoadingDateStrip)
          const Padding(padding: EdgeInsets.all(24), child: Center(child: CircularProgressIndicator()))
        else
          _DateStrip(state: state, controller: controller),
        const Divider(height: 1),
        Expanded(child: _SlotList(state: state, controller: controller)),
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

    return SizedBox(
      height: 72,
      child: ListView.separated(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
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

          return ChoiceChip(
            label: Text(_formatDateLabel(date)),
            selected: selected,
            onSelected: hasAvailability ? (_) => controller.selectDate(date) : null,
            avatar: hasAvailability ? null : const Icon(Icons.block, size: 16),
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
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(state.slotsFailure!.message, textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton(onPressed: controller.retrySlots, child: const Text('Retry')),
            ],
          ),
        ),
      );
    }

    if (state.slots.isEmpty) {
      return Center(
        child: Text('No available slots for this date.', style: Theme.of(context).textTheme.bodyLarge),
      );
    }

    return GridView.builder(
      padding: const EdgeInsets.all(16),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 3,
        mainAxisSpacing: 8,
        crossAxisSpacing: 8,
        childAspectRatio: 2.2,
      ),
      itemCount: state.slots.length,
      itemBuilder: (context, index) {
        final slot = state.slots[index];
        return OutlinedButton(
          onPressed: state.isCreatingHold ? null : () => controller.selectSlot(slot),
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
    final hold = state.activeHold!;
    final textTheme = Theme.of(context).textTheme;

    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.event_available, size: 48, color: Theme.of(context).colorScheme.primary),
            const SizedBox(height: 16),
            Text('Slot held', style: textTheme.titleLarge),
            const SizedBox(height: 8),
            Text(
              '${_formatTime(hold.slotStartUtc)} - ${_formatTime(hold.slotEndUtc)}',
              style: textTheme.bodyLarge,
            ),
            const SizedBox(height: 24),
            Text('Complete booking within', style: textTheme.bodyMedium),
            Text(
              _formatCountdown(state.remainingSeconds),
              style: textTheme.displaySmall,
            ),
            const SizedBox(height: 24),
            OutlinedButton(onPressed: controller.abandonHold, child: const Text('Cancel hold')),
          ],
        ),
      ),
    );
  }
}

class _Banner extends StatelessWidget {
  const _Banner({required this.message, required this.color, required this.onColor, this.onDismiss});

  final String message;
  final Color color;
  final Color onColor;
  final VoidCallback? onDismiss;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      color: color,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      child: Row(
        children: [
          Expanded(child: Text(message, style: TextStyle(color: onColor))),
          if (onDismiss != null)
            IconButton(icon: Icon(Icons.close, color: onColor), onPressed: onDismiss, iconSize: 18),
        ],
      ),
    );
  }
}
