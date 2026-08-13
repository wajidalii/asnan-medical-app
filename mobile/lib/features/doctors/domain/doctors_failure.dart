import 'package:dio/dio.dart';

/// Mirrors AuthFailure's shape/rationale — a single failure type covering
/// every doctors-feature error case, not a family of typed exceptions.
class DoctorsFailure {
  const DoctorsFailure(this.message, {this.isNotFound = false});

  final String message;
  final bool isNotFound;

  factory DoctorsFailure.fromDioException(DioException exception) {
    final response = exception.response;
    if (response == null) {
      return const DoctorsFailure('Network error. Please check your connection and try again.');
    }

    final data = response.data;
    final title = data is Map ? data['title'] as String? : null;

    return switch (response.statusCode) {
      404 => DoctorsFailure(title ?? 'Doctor not found.', isNotFound: true),
      _ => DoctorsFailure(title ?? 'Something went wrong. Please try again.'),
    };
  }
}
