import 'package:dio/dio.dart';

/// Mirrors AppointmentsFailure/ChatFailure's shape/rationale.
class NotificationFailure {
  const NotificationFailure(this.message, {this.isNotFound = false, this.isForbidden = false, this.isBadRequest = false});

  final String message;
  final bool isNotFound;
  final bool isForbidden;
  final bool isBadRequest;

  factory NotificationFailure.fromDioException(DioException exception) {
    final response = exception.response;
    if (response == null) {
      return const NotificationFailure('Network error. Please check your connection and try again.');
    }

    final data = response.data;
    final title = data is Map ? data['title'] as String? : null;

    return switch (response.statusCode) {
      404 => NotificationFailure(title ?? 'Not found.', isNotFound: true),
      403 => const NotificationFailure("You don't have permission to do that.", isForbidden: true),
      400 => NotificationFailure(title ?? 'This notification category cannot be disabled.', isBadRequest: true),
      _ => NotificationFailure(title ?? 'Something went wrong. Please try again.'),
    };
  }
}
