import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/features/auth/domain/auth_failure.dart';

DioException _withResponse(int statusCode, Object? data) {
  final options = RequestOptions(path: '/test');
  return DioException(
    requestOptions: options,
    type: DioExceptionType.badResponse,
    response: Response(requestOptions: options, statusCode: statusCode, data: data),
  );
}

void main() {
  test('no response at all maps to a network-error message', () {
    final failure = AuthFailure.fromDioException(
      DioException(requestOptions: RequestOptions(path: '/test'), type: DioExceptionType.connectionError),
    );

    expect(failure.message, contains('Network error'));
    expect(failure.fieldErrors, isNull);
  });

  test('401 maps to the server-provided title', () {
    final failure = AuthFailure.fromDioException(_withResponse(401, {'title': 'Invalid credentials.'}));

    expect(failure.message, 'Invalid credentials.');
  });

  test('409 maps to a conflict message', () {
    final failure = AuthFailure.fromDioException(_withResponse(409, {'title': 'This account already exists.'}));

    expect(failure.message, 'This account already exists.');
  });

  test('429 maps to a rate-limit message', () {
    final failure = AuthFailure.fromDioException(_withResponse(429, {'title': 'Too many requests.'}));

    expect(failure.message, 'Too many requests.');
  });

  test('validation errors are surfaced as field errors', () {
    final failure = AuthFailure.fromDioException(_withResponse(400, {
      'title': 'One or more validation errors occurred.',
      'errors': {
        'Password': ['This password is too common. Please choose a different one.'],
      },
    }));

    expect(failure.fieldErrors, isNotNull);
    expect(failure.fieldErrors!['Password'], contains('This password is too common. Please choose a different one.'));
  });
}
