import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:asnan/core/network/access_token_holder.dart';
import 'package:asnan/core/network/dio_client.dart';
import 'package:asnan/core/storage/secure_storage_service.dart';
import 'package:asnan/core/theme/app_theme.dart';
import 'package:asnan/features/chat/data/chat_hub_client.dart';
import 'package:asnan/features/chat/domain/chat_connection_status.dart';
import 'package:asnan/features/chat/domain/chat_message.dart';
import 'package:asnan/features/chat/presentation/chat_screen.dart';

import 'fakes/fake_secure_storage_service.dart';

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

ResponseBody _json(Object json, {int statusCode = 200}) => ResponseBody.fromString(
      jsonEncode(json),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

/// Never touches the network — the seam that lets ChatScreen/ChatController
/// be tested deterministically (see chat_hub_client.dart's factory provider).
class _FakeChatHubClient implements ChatHubClient {
  _FakeChatHubClient({this.failToConnect = false});

  final bool failToConnect;
  final joinedConversations = <String>[];
  final sentMessages = <String>[];

  final _messagesController = StreamController<ChatMessage>.broadcast();
  final _readReceiptsController = StreamController<ReadReceipt>.broadcast();
  final _reconnectedController = StreamController<void>.broadcast();
  final _statusController = StreamController<ChatConnectionStatus>.broadcast();

  @override
  Stream<ChatMessage> get messages => _messagesController.stream;

  @override
  Stream<ReadReceipt> get readReceipts => _readReceiptsController.stream;

  @override
  Stream<void> get onReconnected => _reconnectedController.stream;

  @override
  Stream<ChatConnectionStatus> get statusStream => _statusController.stream;

  @override
  Future<void> start() async {
    if (failToConnect) throw Exception('connection refused');
    _statusController.add(ChatConnectionStatus.connected);
  }

  @override
  Future<void> stop() async {
    await _messagesController.close();
    await _readReceiptsController.close();
    await _reconnectedController.close();
    await _statusController.close();
  }

  @override
  Future<void> joinConversation(String conversationId) async {
    joinedConversations.add(conversationId);
  }

  @override
  Future<void> sendMessage(String conversationId, String content) async {
    sentMessages.add(content);
  }

  @override
  Future<void> markAsRead(String conversationId, String lastReadMessageId) async {}

  void emitMessage(ChatMessage message) => _messagesController.add(message);
  void emitReadReceipt(ReadReceipt receipt) => _readReceiptsController.add(receipt);
}

class _FakeAccessTokenHolder extends AccessTokenHolder {
  _FakeAccessTokenHolder(this._token);

  final String? _token;

  @override
  String? build() => _token;
}

/// Header/signature are irrelevant — decodeJwtUserId only reads the payload's `sub` claim.
String _fakeJwt(String userId) {
  String segment(Map<String, dynamic> json) => base64Url.encode(utf8.encode(jsonEncode(json))).replaceAll('=', '');
  return '${segment({
        'alg': 'none'
      })}.${segment({
        'sub': userId
      })}.sig';
}

ChatMessage _message({required String id, required String sender, required String content, DateTime? sentAtUtc}) => ChatMessage(
      id: id,
      chatConversationId: 'conv-1',
      senderUserId: sender,
      content: content,
      sentAtUtc: sentAtUtc ?? DateTime.utc(2026, 8, 14, 10),
    );

Widget _wrap(HttpClientAdapter adapter, _FakeChatHubClient hubClient, {String userId = 'me'}) {
  return ProviderScope(
    overrides: [
      secureStorageProvider.overrideWithValue(FakeSecureStorageService()),
      accessTokenHolderProvider.overrideWith(() => _FakeAccessTokenHolder(_fakeJwt(userId))),
      dioProvider.overrideWith((ref) {
        final dio = Dio(BaseOptions(baseUrl: 'http://test.local'));
        dio.httpClientAdapter = adapter;
        dio.interceptors.add(
          AuthInterceptor(
            dio: dio,
            readAccessToken: () => ref.read(accessTokenHolderProvider),
            onRefresh: () async => null,
          ),
        );
        return dio;
      }),
      chatHubClientFactoryProvider.overrideWithValue(() => hubClient),
    ],
    child: MaterialApp(theme: AppTheme.light, home: const ChatScreen(conversationId: 'conv-1', title: 'Dr. Alice')),
  );
}

void main() {
  testWidgets('renders history and connects with no offline banner', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async {
      expect(options.path, '/chat/conversations/conv-1/messages');
      return _json({
        'messages': [
          {
            'id': 'msg-1',
            'chatConversationId': 'conv-1',
            'senderUserId': 'doctor-1',
            'content': 'Hello there',
            'sentAtUtc': '2026-08-14T09:00:00Z',
          },
        ],
        'nextBeforeCursor': null,
        'hasMore': false,
      });
    });
    final hubClient = _FakeChatHubClient();

    await tester.pumpWidget(_wrap(adapter, hubClient, userId: 'me'));
    await tester.pumpAndSettle();

    expect(find.text('Hello there'), findsOneWidget);
    expect(find.textContaining('Offline'), findsNothing);
    expect(hubClient.joinedConversations, ['conv-1']);
  });

  testWidgets('aligns my messages to the right and the other participant to the left', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async => _json({
          'messages': [
            {
              'id': 'msg-1',
              'chatConversationId': 'conv-1',
              'senderUserId': 'doctor-1',
              'content': 'From doctor',
              'sentAtUtc': '2026-08-14T09:00:00Z',
            },
            {
              'id': 'msg-2',
              'chatConversationId': 'conv-1',
              'senderUserId': 'me',
              'content': 'From me',
              'sentAtUtc': '2026-08-14T09:01:00Z',
            },
          ],
          'nextBeforeCursor': null,
          'hasMore': false,
        }));
    final hubClient = _FakeChatHubClient();

    await tester.pumpWidget(_wrap(adapter, hubClient, userId: 'me'));
    await tester.pumpAndSettle();

    final theirBubble = tester.widget<Align>(
      find.ancestor(of: find.text('From doctor'), matching: find.byType(Align)).first,
    );
    final myBubble = tester.widget<Align>(
      find.ancestor(of: find.text('From me'), matching: find.byType(Align)).first,
    );

    expect(theirBubble.alignment, Alignment.centerLeft);
    expect(myBubble.alignment, Alignment.centerRight);
  });

  testWidgets('a live message arriving over the hub is appended to the list', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async => _json({'messages': [], 'nextBeforeCursor': null, 'hasMore': false}));
    final hubClient = _FakeChatHubClient();

    await tester.pumpWidget(_wrap(adapter, hubClient, userId: 'me'));
    await tester.pumpAndSettle();

    expect(find.text('No messages yet. Say hello!'), findsOneWidget);

    hubClient.emitMessage(_message(id: 'msg-live', sender: 'doctor-1', content: 'Live reply'));
    await tester.pumpAndSettle();

    expect(find.text('Live reply'), findsOneWidget);
  });

  testWidgets('shows a read receipt on my last message once a matching receipt arrives', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async => _json({
          'messages': [
            {
              'id': 'msg-1',
              'chatConversationId': 'conv-1',
              'senderUserId': 'me',
              'content': 'Hi there',
              'sentAtUtc': '2026-08-14T09:00:00Z',
            },
          ],
          'nextBeforeCursor': null,
          'hasMore': false,
        }));
    final hubClient = _FakeChatHubClient();

    await tester.pumpWidget(_wrap(adapter, hubClient, userId: 'me'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Read'), findsNothing);

    hubClient.emitReadReceipt((userId: 'doctor-1', lastReadMessageId: 'msg-1'));
    await tester.pumpAndSettle();

    expect(find.textContaining('· Read'), findsOneWidget);
  });

  testWidgets('a message sent while disconnected shows a Sending… outbox bubble', (tester) async {
    final adapter = _StubHttpClientAdapter((options) async => _json({'messages': [], 'nextBeforeCursor': null, 'hasMore': false}));
    final hubClient = _FakeChatHubClient(failToConnect: true);

    await tester.pumpWidget(_wrap(adapter, hubClient, userId: 'me'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Offline'), findsOneWidget);

    await tester.enterText(find.byType(TextField), 'Queued message');
    await tester.tap(find.byIcon(Icons.send));
    await tester.pumpAndSettle();

    expect(find.text('Queued message'), findsOneWidget);
    expect(find.text('Sending…'), findsOneWidget);
    expect(hubClient.sentMessages, isEmpty);
  });
}
