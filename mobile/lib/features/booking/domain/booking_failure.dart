import 'package:dio/dio.dart';

/// Mirrors AuthFailure/DoctorsFailure's shape/rationale. [isConflict] flags
/// the "someone else booked this slot" case (409) so the UI can show that
/// specific message and refresh the slot list, distinct from a generic error.
class BookingFailure {
  const BookingFailure(this.message, {this.isConflict = false, this.isNotFound = false});

  final String message;
  final bool isConflict;
  final bool isNotFound;

  factory BookingFailure.fromDioException(DioException exception) {
    final response = exception.response;
    if (response == null) {
      return const BookingFailure('Network error. Please check your connection and try again.');
    }

    final data = response.data;
    final title = data is Map ? data['title'] as String? : null;

    return switch (response.statusCode) {
      404 => BookingFailure(title ?? 'Doctor not found.', isNotFound: true),
      409 => BookingFailure(title ?? 'Someone else just booked this slot.', isConflict: true),
      _ => BookingFailure(title ?? 'Something went wrong. Please try again.'),
    };
  }
}
