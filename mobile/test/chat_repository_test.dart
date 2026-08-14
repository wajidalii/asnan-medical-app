import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/features/chat/data/chat_api.dart';
import 'package:asnan/features/chat/data/chat_repository.dart';
import 'package:asnan/features/chat/domain/chat_result.dart';

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

ChatApi _apiWithAdapter(Future<ResponseBody> Function(RequestOptions options) handler) {
  final dio = Dio(BaseOptions(baseUrl: 'http://test.local/api/v1'));
  dio.httpClientAdapter = _StubHttpClientAdapter(handler);
  return ChatApi(dio);
}

ResponseBody _jsonBody(Object json, {int statusCode = 200}) => ResponseBody.fromString(
      jsonEncode(json),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

Map<String, dynamic> _messageJson({String id = 'msg-1', String sender = 'user-1'}) => {
      'id': id,
      'chatConversationId': 'conv-1',
      'senderUserId': sender,
      'content': 'hello',
      'sentAtUtc': '2026-08-14T10:00:00Z',
    };

void main() {
  group('ChatRepository.getMessages', () {
    test('maps a successful response to ChatSuccess with parsed page', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.path, '/chat/conversations/conv-1/messages');
        return _jsonBody({
          'messages': [_messageJson()],
          'nextBeforeCursor': null,
          'hasMore': false,
        });
      });
      final repository = ChatRepository(api);

      final result = await repository.getMessages('conv-1');

      switch (result) {
        case ChatSuccess(:final value):
          expect(value.messages, hasLength(1));
          expect(value.messages.single.id, 'msg-1');
          expect(value.hasMore, isFalse);
        case ChatError():
          fail('expected success');
      }
    });

    test('sends the before cursor as an ISO-8601 UTC query param', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.queryParameters['before'], '2026-08-14T09:00:00.000Z');
        return _jsonBody({'messages': [], 'nextBeforeCursor': null, 'hasMore': false});
      });
      final repository = ChatRepository(api);

      await repository.getMessages('conv-1', before: DateTime.utc(2026, 8, 14, 9));
    });

    test('maps a failure response to ChatError', () async {
      final api = _apiWithAdapter((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 403, data: {'title': 'Forbidden.'}),
        );
      });
      final repository = ChatRepository(api);

      final result = await repository.getMessages('conv-1');

      switch (result) {
        case ChatSuccess():
          fail('expected error');
        case ChatError(:final failure):
          expect(failure.isForbidden, isTrue);
      }
    });
  });

  group('ChatRepository.getReadStatus', () {
    test('maps a successful response to ChatSuccess with parsed status', () async {
      final api = _apiWithAdapter((options) async {
        expect(options.path, '/chat/conversations/conv-1/read-status');
        return _jsonBody({
          'chatConversationId': 'conv-1',
          'lastReadMessageId': 'msg-1',
          'lastReadAtUtc': '2026-08-14T10:00:00Z',
          'unreadCount': 3,
        });
      });
      final repository = ChatRepository(api);

      final result = await repository.getReadStatus('conv-1');

      switch (result) {
        case ChatSuccess(:final value):
          expect(value.unreadCount, 3);
          expect(value.lastReadMessageId, 'msg-1');
        case ChatError():
          fail('expected success');
      }
    });

    test('maps a failure response to ChatError', () async {
      final api = _apiWithAdapter((options) async {
        throw DioException(
          requestOptions: options,
          type: DioExceptionType.badResponse,
          response: Response(requestOptions: options, statusCode: 500, data: {'title': 'Something went wrong.'}),
        );
      });
      final repository = ChatRepository(api);

      final result = await repository.getReadStatus('conv-1');

      switch (result) {
        case ChatSuccess():
          fail('expected error');
        case ChatError(:final failure):
          expect(failure.message, 'Something went wrong.');
      }
    });
  });
}
