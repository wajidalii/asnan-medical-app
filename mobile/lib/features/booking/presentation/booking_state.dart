import '../domain/availability_slot.dart';
import '../domain/booking_failure.dart';
import '../domain/hold.dart';

class _Unset {
  const _Unset();
}

const _unset = _Unset();

class BookingState {
  const BookingState({
    required this.doctorId,
    this.dateAvailability = const {},
    this.isLoadingDateStrip = true,
    this.dateStripFailure,
    this.selectedDate,
    this.slots = const [],
    this.isLoadingSlots = false,
    this.slotsFailure,
    this.isCreatingHold = false,
    this.holdFailure,
    this.activeHold,
    this.remainingSeconds = 0,
    this.holdExpired = false,
  });

  final String doctorId;

  /// Date-only keys (year/month/day, local) -> whether that date has at least one slot.
  final Map<DateTime, bool> dateAvailability;
  final bool isLoadingDateStrip;
  final BookingFailure? dateStripFailure;

  final DateTime? selectedDate;
  final List<AvailabilitySlot> slots;
  final bool isLoadingSlots;
  final BookingFailure? slotsFailure;

  final bool isCreatingHold;
  final BookingFailure? holdFailure;
  final Hold? activeHold;
  final int remainingSeconds;
  final bool holdExpired;

  BookingState copyWith({
    Map<DateTime, bool>? dateAvailability,
    bool? isLoadingDateStrip,
    Object? dateStripFailure = _unset,
    Object? selectedDate = _unset,
    List<AvailabilitySlot>? slots,
    bool? isLoadingSlots,
    Object? slotsFailure = _unset,
    bool? isCreatingHold,
    Object? holdFailure = _unset,
    Object? activeHold = _unset,
    int? remainingSeconds,
    bool? holdExpired,
  }) {
    return BookingState(
      doctorId: doctorId,
      dateAvailability: dateAvailability ?? this.dateAvailability,
      isLoadingDateStrip: isLoadingDateStrip ?? this.isLoadingDateStrip,
      dateStripFailure: identical(dateStripFailure, _unset) ? this.dateStripFailure : dateStripFailure as BookingFailure?,
      selectedDate: identical(selectedDate, _unset) ? this.selectedDate : selectedDate as DateTime?,
      slots: slots ?? this.slots,
      isLoadingSlots: isLoadingSlots ?? this.isLoadingSlots,
      slotsFailure: identical(slotsFailure, _unset) ? this.slotsFailure : slotsFailure as BookingFailure?,
      isCreatingHold: isCreatingHold ?? this.isCreatingHold,
      holdFailure: identical(holdFailure, _unset) ? this.holdFailure : holdFailure as BookingFailure?,
      activeHold: identical(activeHold, _unset) ? this.activeHold : activeHold as Hold?,
      remainingSeconds: remainingSeconds ?? this.remainingSeconds,
      holdExpired: holdExpired ?? this.holdExpired,
    );
  }
}
