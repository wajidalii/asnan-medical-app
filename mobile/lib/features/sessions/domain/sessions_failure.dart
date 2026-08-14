import 'package:dio/dio.dart';

/// Mirrors ProfileFailure/ChatFailure's shape/rationale.
class SessionsFailure {
  const SessionsFailure(this.message, {this.isNotFound = false});

  final String message;
  final bool isNotFound;

  factory SessionsFailure.fromDioException(DioException exception) {
    final response = exception.response;
    if (response == null) {
      return const SessionsFailure('Network error. Please check your connection and try again.');
    }

    final data = response.data;
    final title = data is Map ? data['title'] as String? : null;

    return switch (response.statusCode) {
      404 => SessionsFailure(title ?? 'That session was already signed out.', isNotFound: true),
      _ => SessionsFailure(title ?? 'Something went wrong. Please try again.'),
    };
  }
}
