import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/dio_client.dart';
import '../domain/doctor_availability.dart';
import '../domain/hold.dart';

/// Thin wrapper over the raw HTTP calls — same convention as DoctorsApi.
/// getAvailability is public; createHold requires auth, which the shared
/// intercepted dioProvider client already attaches when a session exists.
class BookingApi {
  BookingApi(this._dio);

  final Dio _dio;

  Future<DoctorAvailability> getAvailability(String doctorId, DateTime date) async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/availability/doctors/$doctorId',
      queryParameters: {'date': _formatDate(date)},
    );
    return DoctorAvailability.fromJson(response.data!);
  }

  Future<Hold> createHold(String doctorId, DateTime slotStartUtc, DateTime slotEndUtc) async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/appointments/holds',
      data: {
        'doctorId': doctorId,
        'slotStartUtc': slotStartUtc.toUtc().toIso8601String(),
        'slotEndUtc': slotEndUtc.toUtc().toIso8601String(),
      },
    );
    return Hold.fromJson(response.data!);
  }

  static String _formatDate(DateTime date) {
    final y = date.year.toString().padLeft(4, '0');
    final m = date.month.toString().padLeft(2, '0');
    final d = date.day.toString().padLeft(2, '0');
    return '$y-$m-$d';
  }
}

final bookingApiProvider = Provider<BookingApi>((ref) => BookingApi(ref.watch(dioProvider)));
