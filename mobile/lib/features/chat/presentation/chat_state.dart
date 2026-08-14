import '../domain/chat_connection_status.dart';
import '../domain/chat_failure.dart';
import '../domain/chat_message.dart';
import '../domain/outbox_message.dart';

class _Unset {
  const _Unset();
}

const _unset = _Unset();

class ChatState {
  const ChatState({
    this.isLoadingHistory = true,
    this.historyFailure,
    this.messages = const [],
    this.hasMoreHistory = false,
    this.isLoadingMoreHistory = false,
    this.oldestCursor,
    this.connectionStatus = ChatConnectionStatus.connecting,
    this.outbox = const [],
    this.otherLastReadMessageId,
  });

  final bool isLoadingHistory;
  final ChatFailure? historyFailure;
  final List<ChatMessage> messages;
  final bool hasMoreHistory;
  final bool isLoadingMoreHistory;
  final DateTime? oldestCursor;

  final ChatConnectionStatus connectionStatus;

  /// Locally-composed messages not yet confirmed sent — see OutboxMessage's doc comment.
  final List<OutboxMessage> outbox;

  /// The other participant's live read-receipt position (only known once at least one arrives while connected — see ChatController's doc comment).
  final String? otherLastReadMessageId;

  ChatState copyWith({
    bool? isLoadingHistory,
    Object? historyFailure = _unset,
    List<ChatMessage>? messages,
    bool? hasMoreHistory,
    bool? isLoadingMoreHistory,
    Object? oldestCursor = _unset,
    ChatConnectionStatus? connectionStatus,
    List<OutboxMessage>? outbox,
    Object? otherLastReadMessageId = _unset,
  }) {
    return ChatState(
      isLoadingHistory: isLoadingHistory ?? this.isLoadingHistory,
      historyFailure: identical(historyFailure, _unset) ? this.historyFailure : historyFailure as ChatFailure?,
      messages: messages ?? this.messages,
      hasMoreHistory: hasMoreHistory ?? this.hasMoreHistory,
      isLoadingMoreHistory: isLoadingMoreHistory ?? this.isLoadingMoreHistory,
      oldestCursor: identical(oldestCursor, _unset) ? this.oldestCursor : oldestCursor as DateTime?,
      connectionStatus: connectionStatus ?? this.connectionStatus,
      outbox: outbox ?? this.outbox,
      otherLastReadMessageId: identical(otherLastReadMessageId, _unset) ? this.otherLastReadMessageId : otherLastReadMessageId as String?,
    );
  }
}
