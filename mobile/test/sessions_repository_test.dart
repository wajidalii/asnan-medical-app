import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/features/sessions/data/sessions_api.dart';
import 'package:asnan/features/sessions/data/sessions_repository.dart';
import 'package:asnan/features/sessions/domain/sessions_result.dart';

class _StubHttpClientAdapter implements HttpClientAdapter {
  _StubHttpClientAdapter(this._handler);

  final Future<ResponseBody> Function(RequestOptions options) _handler;

  @override
  void close({bool force = false}) {}

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) =>
      _handler(options);
}

SessionsApi _apiWithAdapter(Future<ResponseBody> Function(RequestOptions options) handler) {
  final dio = Dio(BaseOptions(baseUrl: 'http://test.local/api/v1'));
  dio.httpClientAdapter = _StubHttpClientAdapter(handler);
  return SessionsApi(dio);
}

ResponseBody _jsonBody(Object json, {int statusCode = 200}) => ResponseBody.fromString(
      jsonEncode(json),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

void main() {
  group('SessionsRepository.getSessions', () {
    test('maps a successful response to a list of sessions', () async {
      final api = _apiWithAdapter((options) async => _jsonBody([
            {
              'id': 'session-1',
              'deviceId': 'device-1',
              'deviceName': 'iPhone 15',
              'lastSeenAtUtc': '2026-08-14T09:00:00Z',
              'absoluteExpiresAtUtc': '2026-11-14T09:00:00Z',
            },
          ]));
      final repository = SessionsRepository(api);

      final result = await repository.getSessions();

      switch (result) {
        case SessionsSuccess(:final value):
          expect(value, hasLength(1));
          expect(value.single.deviceName, 'iPhone 15');
        case SessionsError():
          fail('expected success');
      }
    });

    test('maps a failure response to SessionsError', () async {
      final api = _apiWithAdapter((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 500, data: {'title': 'Something went wrong.'}),
        );
      });
      final repository = SessionsRepository(api);

      final result = await repository.getSessions();

      switch (result) {
        case SessionsSuccess():
          fail('expected error');
        case SessionsError(:final failure):
          expect(failure.message, 'Something went wrong.');
      }
    });
  });

  group('SessionsRepository.revokeSession', () {
    test('sends a DELETE to the session-specific endpoint', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.method, 'DELETE');
        expect(options.path, '/auth/sessions/session-1');
        return ResponseBody.fromString('', 204);
      });
      final repository = SessionsRepository(api);

      final result = await repository.revokeSession('session-1');

      expect(result, isA<SessionsSuccess>());
    });

    test('maps a 404 (already revoked) to SessionsError', () async {
      final api = _apiWithAdapter((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 404, data: {'title': 'Not found.'}),
        );
      });
      final repository = SessionsRepository(api);

      final result = await repository.revokeSession('session-1');

      switch (result) {
        case SessionsSuccess():
          fail('expected error');
        case SessionsError(:final failure):
          expect(failure.isNotFound, isTrue);
      }
    });
  });
}
