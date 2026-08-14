import 'dart:async';

import 'package:signalr_netcore/iretry_policy.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../../../core/network/api_config.dart';
import '../domain/chat_connection_status.dart';
import '../domain/chat_message.dart';
import 'chat_hub_client.dart';
import 'exponential_backoff_retry_policy.dart';

const _hubPath = '/hubs/chat';

/// Wraps `signalr_netcore`'s HubConnection behind [ChatHubClient]'s
/// app-level streams/methods — issue #29.
///
/// [accessTokenFactory] is re-invoked on every (re)connect attempt rather
/// than captured once, so a token refreshed elsewhere in the app (the
/// existing Dio 401-retry flow) is picked up automatically — this is what
/// "handles token refresh mid-connection" means in practice: the *next*
/// reconnect uses whatever's current, there is no way to swap the token of
/// an already-established connection out from under it.
class SignalrChatHubClient implements ChatHubClient {
  SignalrChatHubClient({required Future<String> Function() accessTokenFactory, IRetryPolicy? reconnectPolicy}) {
    final apiRoot = apiBaseUrl.replaceFirst('/api/v1', '');
    _connection = HubConnectionBuilder()
        .withUrl(
          '$apiRoot$_hubPath',
          options: HttpConnectionOptions(accessTokenFactory: accessTokenFactory),
        )
        .withAutomaticReconnect(reconnectPolicy: reconnectPolicy ?? const ExponentialBackoffRetryPolicy())
        .build();

    _connection.on('ReceiveMessage', (arguments) {
      if (arguments == null || arguments.isEmpty) return;
      _messagesController.add(ChatMessage.fromJson((arguments[0] as Map).cast<String, dynamic>()));
    });

    _connection.on('MessagesRead', (arguments) {
      if (arguments == null || arguments.length < 2) return;
      _readReceiptsController.add((userId: arguments[0] as String, lastReadMessageId: arguments[1] as String));
    });

    _connection.onreconnecting(({Exception? error}) => _statusController.add(ChatConnectionStatus.reconnecting));
    _connection.onreconnected(({String? connectionId}) {
      _statusController.add(ChatConnectionStatus.connected);
      _reconnectedController.add(null);
    });
    _connection.onclose(({Exception? error}) => _statusController.add(ChatConnectionStatus.disconnected));
  }

  late final HubConnection _connection;
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
    _statusController.add(ChatConnectionStatus.connecting);
    await _connection.start();
    _statusController.add(ChatConnectionStatus.connected);
  }

  @override
  Future<void> stop() async {
    await _connection.stop();
    await _messagesController.close();
    await _readReceiptsController.close();
    await _reconnectedController.close();
    await _statusController.close();
  }

  @override
  Future<void> joinConversation(String conversationId) => _connection.invoke('JoinConversation', args: [conversationId]);

  @override
  Future<void> sendMessage(String conversationId, String content) =>
      _connection.invoke('SendMessage', args: [conversationId, content]);

  @override
  Future<void> markAsRead(String conversationId, String lastReadMessageId) =>
      _connection.invoke('MarkAsRead', args: [conversationId, lastReadMessageId]);
}
