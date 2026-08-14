import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/features/notifications/data/notifications_api.dart';
import 'package:asnan/features/notifications/data/notifications_repository.dart';
import 'package:asnan/features/notifications/domain/device_platform.dart';
import 'package:asnan/features/notifications/domain/notification_category.dart';
import 'package:asnan/features/notifications/domain/notification_result.dart';

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

NotificationsApi _apiWithAdapter(Future<ResponseBody> Function(RequestOptions options) handler) {
  final dio = Dio(BaseOptions(baseUrl: 'http://test.local/api/v1'));
  dio.httpClientAdapter = _StubHttpClientAdapter(handler);
  return NotificationsApi(dio);
}

ResponseBody _jsonBody(Object json, {int statusCode = 200}) => ResponseBody.fromString(
      jsonEncode(json),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

void main() {
  group('NotificationsRepository.registerDevice', () {
    test('posts the token and platform value', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.path, '/notifications/devices');
        expect(options.data, {'fcmToken': 'tok-1', 'platform': 1});
        return ResponseBody.fromString('', 204);
      });
      final repository = NotificationsRepository(api);

      final result = await repository.registerDevice('tok-1', DevicePlatform.android);

      expect(result, isA<NotificationSuccess>());
    });
  });

  group('NotificationsRepository.removeDevice', () {
    test('sends a DELETE with the token in the body', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.method, 'DELETE');
        expect(options.data, {'fcmToken': 'tok-1'});
        return ResponseBody.fromString('', 204);
      });
      final repository = NotificationsRepository(api);

      final result = await repository.removeDevice('tok-1');

      expect(result, isA<NotificationSuccess>());
    });
  });

  group('NotificationsRepository.getPreferences', () {
    test('maps a successful response to a list of preferences', () async {
      final api = _apiWithAdapter((options) async => _jsonBody([
            {'category': 1, 'isEnabled': true, 'isDisableable': false},
            {'category': 3, 'isEnabled': false, 'isDisableable': true},
          ]));
      final repository = NotificationsRepository(api);

      final result = await repository.getPreferences();

      switch (result) {
        case NotificationSuccess(:final value):
          expect(value, hasLength(2));
          expect(value[0].category, NotificationCategory.appointmentUpdates);
          expect(value[1].isEnabled, isFalse);
        case NotificationError():
          fail('expected success');
      }
    });

    test('maps a failure response to NotificationError', () async {
      final api = _apiWithAdapter((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 500, data: {'title': 'Something went wrong.'}),
        );
      });
      final repository = NotificationsRepository(api);

      final result = await repository.getPreferences();

      switch (result) {
        case NotificationSuccess():
          fail('expected error');
        case NotificationError(:final failure):
          expect(failure.message, 'Something went wrong.');
      }
    });
  });

  group('NotificationsRepository.setPreference', () {
    test('PUTs to the category-specific endpoint', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.path, '/notifications/preferences/reminders');
        expect(options.data, {'isEnabled': false});
        return ResponseBody.fromString('', 204);
      });
      final repository = NotificationsRepository(api);

      final result = await repository.setPreference(NotificationCategory.reminders, false);

      expect(result, isA<NotificationSuccess>());
    });

    test('maps a 400 (non-disableable category) to NotificationError', () async {
      final api = _apiWithAdapter((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 400, data: {'title': 'This notification category cannot be disabled.'}),
        );
      });
      final repository = NotificationsRepository(api);

      final result = await repository.setPreference(NotificationCategory.appointmentUpdates, false);

      switch (result) {
        case NotificationSuccess():
          fail('expected error');
        case NotificationError(:final failure):
          expect(failure.isBadRequest, isTrue);
      }
    });
  });
}
