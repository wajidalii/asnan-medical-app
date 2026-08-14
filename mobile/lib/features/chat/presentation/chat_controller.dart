import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:uuid/uuid.dart';

import '../data/chat_hub_client.dart';
import '../data/chat_repository.dart';
import '../domain/chat_connection_status.dart';
import '../domain/chat_message_page.dart';
import '../domain/chat_result.dart';
import '../domain/outbox_message.dart';
import 'chat_state.dart';

const _uuid = Uuid();

/// Per-conversation chat state — history, live messages, connection
/// lifecycle, outbox, and read receipts. A fresh ChatHubClient is created
/// per instance ("connect on entering a conversation"), torn down on dispose.
///
/// Known limitation: the other participant's read position is only known
/// from *live* MarkAsRead receipts received while this screen is open —
/// there's no historical "what has the other person already read" REST
/// lookup, since the backend's read-status endpoint only reports the
/// caller's own position (#28's design). Documented, not silently assumed.
class ChatController extends FamilyNotifier<ChatState, String> {
  ChatHubClient? _hubClient;
  StreamSubscription? _messagesSub;
  StreamSubscription? _readReceiptsSub;
  StreamSubscription? _reconnectedSub;
  StreamSubscription? _statusSub;
  String? _myUserId;

  @override
  ChatState build(String conversationId) {
    ref.onDispose(() {
      _messagesSub?.cancel();
      _readReceiptsSub?.cancel();
      _reconnectedSub?.cancel();
      _statusSub?.cancel();
      _hubClient?.stop();
    });
    return const ChatState();
  }

  /// Called once from the screen's initState — same convention as BookingController.loadDateStrip().
  Future<void> initialize(String myUserId) async {
    _myUserId = myUserId;
    await _loadHistory();
    await _connect();
  }

  Future<void> _loadHistory() async {
    state = state.copyWith(isLoadingHistory: true, historyFailure: null);

    final result = await ref.read(chatRepositoryProvider).getMessages(arg);

    switch (result) {
      case ChatSuccess<ChatMessagePage>(:final value):
        state = state.copyWith(
          isLoadingHistory: false,
          messages: value.messages,
          hasMoreHistory: value.hasMore,
          oldestCursor: value.nextBeforeCursor,
        );
      case ChatError<ChatMessagePage>(:final failure):
        state = state.copyWith(isLoadingHistory: false, historyFailure: failure);
    }
  }

  Future<void> retryLoadHistory() => _loadHistory();

  Future<void> loadMoreHistory() async {
    if (!state.hasMoreHistory || state.isLoadingMoreHistory || state.oldestCursor == null) return;

    state = state.copyWith(isLoadingMoreHistory: true);
    final result = await ref.read(chatRepositoryProvider).getMessages(arg, before: state.oldestCursor);

    switch (result) {
      case ChatSuccess<ChatMessagePage>(:final value):
        state = state.copyWith(
          isLoadingMoreHistory: false,
          messages: [...value.messages, ...state.messages],
          hasMoreHistory: value.hasMore,
          oldestCursor: value.nextBeforeCursor,
        );
      case ChatError<ChatMessagePage>():
        // Keep the existing history visible — same "load more failures don't clear the list" rule as the doctor/appointment lists.
        state = state.copyWith(isLoadingMoreHistory: false);
    }
  }

  Future<void> _connect() async {
    final client = ref.read(chatHubClientFactoryProvider)();
    _hubClient = client;

    _messagesSub = client.messages.listen((message) {
      state = state.copyWith(messages: [...state.messages, message]);
      if (message.senderUserId != _myUserId) {
        markLatestAsRead();
      }
    });

    _readReceiptsSub = client.readReceipts.listen((receipt) {
      if (receipt.userId != _myUserId) {
        state = state.copyWith(otherLastReadMessageId: receipt.lastReadMessageId);
      }
    });

    _statusSub = client.statusStream.listen((status) => state = state.copyWith(connectionStatus: status));

    _reconnectedSub = client.onReconnected.listen((_) async {
      // SignalR group membership isn't restored across a reconnect — must rejoin explicitly.
      await client.joinConversation(arg);
      await _flushOutbox();
    });

    try {
      await client.start();
      await client.joinConversation(arg);
      await markLatestAsRead();
    } catch (_) {
      state = state.copyWith(connectionStatus: ChatConnectionStatus.disconnected);
    }
  }

  /// Queues locally, then sends immediately if connected — otherwise stays
  /// queued and is flushed on the next reconnect. Removed from the outbox
  /// only once the SendMessage invocation itself resolves successfully; the
  /// authoritative copy then arrives normally via the live broadcast, so it
  /// never appears in both the outbox and the message list at once.
  Future<void> sendMessage(String content) async {
    final trimmed = content.trim();
    if (trimmed.isEmpty) return;

    final item = OutboxMessage(localId: _uuid.v4(), content: trimmed, createdAtUtc: DateTime.now().toUtc());
    state = state.copyWith(outbox: [...state.outbox, item]);

    await _trySend(item);
  }

  Future<void> _trySend(OutboxMessage item) async {
    if (state.connectionStatus != ChatConnectionStatus.connected) {
      return; // stays queued — flushed on reconnect
    }

    final client = _hubClient;
    if (client == null) return;

    try {
      await client.sendMessage(arg, item.content);
      state = state.copyWith(outbox: state.outbox.where((o) => o.localId != item.localId).toList());
    } catch (_) {
      // Left queued for retry on the next reconnect (or the user can be shown a "failed" affordance via item.failed if desired later).
    }
  }

  Future<void> _flushOutbox() async {
    for (final item in List<OutboxMessage>.of(state.outbox)) {
      await _trySend(item);
    }
  }

  Future<void> markLatestAsRead() async {
    final client = _hubClient;
    if (client == null || state.messages.isEmpty) return;

    try {
      await client.markAsRead(arg, state.messages.last.id);
    } catch (_) {
      // Best-effort — a missed read receipt just means the unread badge stays stale a bit longer, nothing is lost.
    }
  }
}

final chatControllerProvider = NotifierProvider.family<ChatController, ChatState, String>(ChatController.new);
