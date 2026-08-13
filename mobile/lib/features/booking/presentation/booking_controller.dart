import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/booking_repository.dart';
import '../domain/availability_slot.dart';
import '../domain/booking_result.dart';
import '../domain/doctor_availability.dart';
import '../domain/hold.dart';
import 'booking_state.dart';

const _dateStripDays = 14;

/// Per-doctor booking flow state: date strip -> slot list -> hold -> countdown.
/// A family provider since the flow is scoped to whichever doctor the patient
/// is booking, same as DoctorDetailScreen's per-id provider from #14.
class BookingController extends FamilyNotifier<BookingState, String> {
  Timer? _countdownTimer;

  @override
  BookingState build(String doctorId) {
    ref.onDispose(() => _countdownTimer?.cancel());
    return BookingState(doctorId: doctorId);
  }

  /// Called once from the screen's initState — same convention as
  /// AuthController.restoreSession() / DoctorListController.loadInitial().
  Future<void> loadDateStrip() async {
    state = state.copyWith(isLoadingDateStrip: true);

    final repo = ref.read(bookingRepositoryProvider);
    final today = DateTime.now();
    final dates = List.generate(_dateStripDays, (i) => DateTime(today.year, today.month, today.day + i));

    final results = await Future.wait(dates.map((d) => repo.getAvailability(state.doctorId, d)));

    final availability = <DateTime, bool>{};
    for (var i = 0; i < dates.length; i++) {
      availability[dates[i]] = switch (results[i]) {
        BookingSuccess<DoctorAvailability>(:final value) => value.slots.isNotEmpty,
        BookingError<DoctorAvailability>() => false,
      };
    }

    state = state.copyWith(dateAvailability: availability, isLoadingDateStrip: false);

    final availableDates = dates.where((d) => availability[d] == true);
    final firstAvailable = availableDates.isEmpty ? dates.first : availableDates.first;
    await selectDate(firstAvailable);
  }

  Future<void> selectDate(DateTime date) async {
    state = state.copyWith(selectedDate: date, isLoadingSlots: true, slotsFailure: null);

    final result = await ref.read(bookingRepositoryProvider).getAvailability(state.doctorId, date);

    switch (result) {
      case BookingSuccess<DoctorAvailability>(:final value):
        state = state.copyWith(slots: value.slots, isLoadingSlots: false);
      case BookingError<DoctorAvailability>(:final failure):
        state = state.copyWith(slots: const [], isLoadingSlots: false, slotsFailure: failure);
    }
  }

  Future<void> retrySlots() async {
    final date = state.selectedDate;
    if (date != null) {
      await selectDate(date);
    }
  }

  Future<void> selectSlot(AvailabilitySlot slot) async {
    state = state.copyWith(isCreatingHold: true, holdFailure: null, holdExpired: false);

    final result = await ref.read(bookingRepositoryProvider).createHold(state.doctorId, slot.startUtc, slot.endUtc);

    switch (result) {
      case BookingSuccess<Hold>(:final value):
        state = state.copyWith(
          isCreatingHold: false,
          activeHold: value,
          remainingSeconds: value.expiresAtUtc.difference(DateTime.now().toUtc()).inSeconds,
        );
        _startCountdown();
      case BookingError<Hold>(:final failure):
        state = state.copyWith(isCreatingHold: false, holdFailure: failure);
        // A conflict means someone else just took it — refresh so the taken
        // slot disappears from the list instead of staying selectable.
        if (failure.isConflict) {
          await retrySlots();
        }
    }
  }

  void _startCountdown() {
    _countdownTimer?.cancel();
    _countdownTimer = Timer.periodic(const Duration(seconds: 1), (_) => _tick());
  }

  void _tick() {
    final hold = state.activeHold;
    if (hold == null) {
      _countdownTimer?.cancel();
      return;
    }

    final remaining = hold.expiresAtUtc.difference(DateTime.now().toUtc()).inSeconds;
    if (remaining <= 0) {
      _countdownTimer?.cancel();
      _expireHold();
      return;
    }

    state = state.copyWith(remainingSeconds: remaining);
  }

  Future<void> _expireHold() async {
    state = state.copyWith(activeHold: null, remainingSeconds: 0, holdExpired: true);
    await retrySlots();
  }

  /// Explicit release before expiry (user backs out) — no server-side
  /// release endpoint exists yet (out of scope for #17), so this only clears
  /// local state; the hold still lapses server-side at its TTL.
  void abandonHold() {
    _countdownTimer?.cancel();
    state = state.copyWith(activeHold: null, remainingSeconds: 0, holdExpired: false);
  }

  void dismissExpiredNotice() {
    state = state.copyWith(holdExpired: false);
  }
}

final bookingControllerProvider =
    NotifierProvider.family<BookingController, BookingState, String>(BookingController.new);
