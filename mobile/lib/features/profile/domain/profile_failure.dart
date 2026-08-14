import 'package:dio/dio.dart';

/// Mirrors ChatFailure/NotificationFailure's shape/rationale.
class ProfileFailure {
  const ProfileFailure(this.message, {this.isNotFound = false, this.isBadRequest = false});

  final String message;
  final bool isNotFound;
  final bool isBadRequest;

  factory ProfileFailure.fromDioException(DioException exception) {
    final response = exception.response;
    if (response == null) {
      return const ProfileFailure('Network error. Please check your connection and try again.');
    }

    final data = response.data;
    final title = data is Map ? data['title'] as String? : null;

    return switch (response.statusCode) {
      404 => ProfileFailure(title ?? 'Not found.', isNotFound: true),
      400 => ProfileFailure(title ?? 'Please check the highlighted fields.', isBadRequest: true),
      _ => ProfileFailure(title ?? 'Something went wrong. Please try again.'),
    };
  }
}
