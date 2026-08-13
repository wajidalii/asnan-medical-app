import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/booking_failure.dart';
import '../domain/booking_result.dart';
import '../domain/doctor_availability.dart';
import '../domain/hold.dart';
import 'booking_api.dart';

class BookingRepository {
  BookingRepository(this._api);

  final BookingApi _api;

  Future<BookingResult<DoctorAvailability>> getAvailability(String doctorId, DateTime date) =>
      _guard(() => _api.getAvailability(doctorId, date));

  Future<BookingResult<Hold>> createHold(String doctorId, DateTime slotStartUtc, DateTime slotEndUtc) =>
      _guard(() => _api.createHold(doctorId, slotStartUtc, slotEndUtc));

  Future<BookingResult<T>> _guard<T>(Future<T> Function() action) async {
    try {
      return BookingSuccess(await action());
    } on DioException catch (e) {
      return BookingError(BookingFailure.fromDioException(e));
    }
  }
}

final bookingRepositoryProvider = Provider<BookingRepository>((ref) => BookingRepository(ref.watch(bookingApiProvider)));
