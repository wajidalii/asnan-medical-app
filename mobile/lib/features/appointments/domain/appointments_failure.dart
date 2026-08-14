import 'package:dio/dio.dart';

/// Mirrors BookingFailure/PaymentFailure's shape/rationale.
class AppointmentsFailure {
  const AppointmentsFailure(this.message, {this.isNotFound = false, this.isForbidden = false, this.isConflict = false});

  final String message;
  final bool isNotFound;
  final bool isForbidden;
  final bool isConflict;

  factory AppointmentsFailure.fromDioException(DioException exception) {
    final response = exception.response;
    if (response == null) {
      return const AppointmentsFailure('Network error. Please check your connection and try again.');
    }

    final data = response.data;
    final title = data is Map ? data['title'] as String? : null;

    return switch (response.statusCode) {
      404 => AppointmentsFailure(title ?? 'Appointment not found.', isNotFound: true),
      403 => const AppointmentsFailure("You don't have permission to do that.", isForbidden: true),
      409 => AppointmentsFailure(title ?? 'This appointment cannot be cancelled.', isConflict: true),
      _ => AppointmentsFailure(title ?? 'Something went wrong. Please try again.'),
    };
  }
}
