import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/access_token_holder.dart';
import '../domain/chat_connection_status.dart';
import '../domain/chat_message.dart';
import 'signalr_chat_hub_client.dart';

/// One record per live "X read up to message Y" receipt from the other participant.
typedef ReadReceipt = ({String userId, String lastReadMessageId});

/// App-level interface over the real-time chat connection — issue #29. Kept
/// separate from SignalrChatHubClient (the `signalr_netcore`-backed
/// implementation) purely so a fake can stand in for tests, which have no
/// network to talk to a real hub — same rationale as CalendarService (#26).
///
/// A fresh instance is created per conversation screen (matches "connect on
/// entering a conversation"), not a single app-wide connection.
abstract class ChatHubClient {
  Stream<ChatMessage> get messages;

  Stream<ReadReceipt> get readReceipts;

  /// Fires after every successful reconnect (not the initial connect) — the outbox flush / rejoin hook.
  Stream<void> get onReconnected;

  Stream<ChatConnectionStatus> get statusStream;

  Future<void> start();

  Future<void> stop();

  Future<void> joinConversation(String conversationId);

  Future<void> sendMessage(String conversationId, String content);

  Future<void> markAsRead(String conversationId, String lastReadMessageId);
}

/// A factory rather than a cached instance — ChatController needs a *fresh*
/// client every time it connects, not one reused/shared across conversations
/// or reconnect attempts.
final chatHubClientFactoryProvider = Provider<ChatHubClient Function()>((ref) {
  return () => SignalrChatHubClient(accessTokenFactory: () async => ref.read(accessTokenHolderProvider) ?? '');
});
