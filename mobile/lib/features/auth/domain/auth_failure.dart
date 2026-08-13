import 'package:dio/dio.dart';

/// A single failure shape covering every auth-flow error case (network,
/// validation, invalid credentials, rate limiting, conflict). Deliberately
/// not a family of typed exceptions — the UI only ever needs a message to
/// show and, for validation errors, which fields to flag; splitting this
/// into many failure subclasses wasn't earning its keep at this scope.
class AuthFailure {
  const AuthFailure(this.message, {this.fieldErrors});

  final String message;
  final Map<String, List<String>>? fieldErrors;

  factory AuthFailure.fromDioException(DioException exception) {
    final response = exception.response;
    if (response == null) {
      return const AuthFailure(
        'Network error. Please check your connection and try again.',
      );
    }

    final data = response.data;
    final title = data is Map ? data['title'] as String? : null;

    if (data is Map && data['errors'] is Map) {
      final rawErrors = data['errors'] as Map;
      final fieldErrors = <String, List<String>>{
        for (final entry in rawErrors.entries)
          entry.key as String: List<String>.from(entry.value as List),
      };
      return AuthFailure(
        title ?? 'Please fix the highlighted fields.',
        fieldErrors: fieldErrors,
      );
    }

    return switch (response.statusCode) {
      401 => AuthFailure(title ?? 'Invalid credentials.'),
      409 => AuthFailure(title ?? 'This account already exists.'),
      429 => AuthFailure(title ?? 'Too many attempts. Please wait and try again.'),
      _ => AuthFailure(title ?? 'Something went wrong. Please try again.'),
    };
  }
}
