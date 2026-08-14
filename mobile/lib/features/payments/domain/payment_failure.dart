import 'package:dio/dio.dart';

/// Mirrors BookingFailure/DoctorsFailure's shape/rationale.
class PaymentFailure {
  const PaymentFailure(this.message, {this.isNotFound = false, this.isConflict = false});

  final String message;
  final bool isNotFound;
  final bool isConflict;

  factory PaymentFailure.fromDioException(DioException exception) {
    final response = exception.response;
    if (response == null) {
      return const PaymentFailure('Network error. Please check your connection and try again.');
    }

    final data = response.data;
    final title = data is Map ? data['title'] as String? : null;

    return switch (response.statusCode) {
      404 => PaymentFailure(title ?? 'Not found.', isNotFound: true),
      409 => PaymentFailure(title ?? 'This hold has expired.', isConflict: true),
      _ => PaymentFailure(title ?? 'Something went wrong. Please try again.'),
    };
  }
}
