import 'package:dio/dio.dart';

/// Mirrors AppointmentsFailure/PaymentFailure's shape/rationale — for the REST side (message history, read status). Hub-invoke failures are surfaced separately (they're not DioExceptions).
class ChatFailure {
  const ChatFailure(this.message, {this.isNotFound = false, this.isForbidden = false});

  final String message;
  final bool isNotFound;
  final bool isForbidden;

  factory ChatFailure.fromDioException(DioException exception) {
    final response = exception.response;
    if (response == null) {
      return const ChatFailure('Network error. Please check your connection and try again.');
    }

    final data = response.data;
    final title = data is Map ? data['title'] as String? : null;

    return switch (response.statusCode) {
      404 => ChatFailure(title ?? 'Conversation not found.', isNotFound: true),
      403 => const ChatFailure("You don't have permission to view this conversation.", isForbidden: true),
      _ => ChatFailure(title ?? 'Something went wrong. Please try again.'),
    };
  }
}
