import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/current_user.dart';
import '../domain/chat_connection_status.dart';
import '../domain/chat_message.dart';
import '../domain/outbox_message.dart';
import 'chat_controller.dart';
import 'chat_state.dart';

const _reconnectAmberLight = Color(0xFFB8863C);
const _reconnectAmberDark = Color(0xFFD1A35E);
const _reconnectBgLight = Color(0xFFF6ECD9);
const _reconnectBgDark = Color(0xFF3A2F1C);

String _formatTime(DateTime utc) {
  final local = utc.toLocal();
  final hour12 = local.hour % 12 == 0 ? 12 : local.hour % 12;
  final minute = local.minute.toString().padLeft(2, '0');
  final period = local.hour < 12 ? 'AM' : 'PM';
  return '$hour12:$minute $period';
}

String _connectionLabel(ChatConnectionStatus status) => switch (status) {
      ChatConnectionStatus.connecting => 'Connecting…',
      ChatConnectionStatus.connected => 'Connected',
      ChatConnectionStatus.reconnecting => 'Reconnecting…',
      ChatConnectionStatus.disconnected => 'Offline — messages will send once reconnected',
    };

/// Chat for one appointment's conversation (#29) — connects on entering,
/// shows history + live messages in order, read receipts, and an outbox
/// for messages composed while disconnected.
class ChatScreen extends ConsumerStatefulWidget {
  const ChatScreen({super.key, required this.conversationId, required this.title});

  final String conversationId;
  final String title;

  @override
  ConsumerState<ChatScreen> createState() => _ChatScreenState();
}

class _ChatScreenState extends ConsumerState<ChatScreen> {
  final _inputController = TextEditingController();
  final _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final myUserId = ref.read(currentUserIdProvider);
      if (myUserId != null) {
        ref.read(chatControllerProvider(widget.conversationId).notifier).initialize(myUserId);
      }
    });
    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scrollController.removeListener(_onScroll);
    _scrollController.dispose();
    _inputController.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (_scrollController.position.pixels <= 100) {
      ref.read(chatControllerProvider(widget.conversationId).notifier).loadMoreHistory();
    }
  }

  void _send() {
    final text = _inputController.text;
    if (text.trim().isEmpty) return;
    ref.read(chatControllerProvider(widget.conversationId).notifier).sendMessage(text);
    _inputController.clear();
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(chatControllerProvider(widget.conversationId));
    final controller = ref.read(chatControllerProvider(widget.conversationId).notifier);
    final myUserId = ref.watch(currentUserIdProvider);
    final isDark = Theme.of(context).brightness == Brightness.dark;

    ref.listen(chatControllerProvider(widget.conversationId), (previous, next) {
      final grew = next.messages.length > (previous?.messages.length ?? 0);
      if (grew && _scrollController.hasClients) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (_scrollController.hasClients) {
            _scrollController.jumpTo(_scrollController.position.maxScrollExtent);
          }
        });
      }
    });

    return Scaffold(
      appBar: AppBar(title: Text(widget.title)),
      body: Column(
        children: [
          if (state.connectionStatus != ChatConnectionStatus.connected)
            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(vertical: 8),
              decoration: BoxDecoration(
                color: isDark ? _reconnectBgDark : _reconnectBgLight,
                border: Border(bottom: BorderSide(color: isDark ? _reconnectAmberDark : _reconnectAmberLight)),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Icons.sync, size: 13, color: isDark ? _reconnectAmberDark : _reconnectAmberLight),
                  const SizedBox(width: 7),
                  Text(
                    _connectionLabel(state.connectionStatus),
                    style: Theme.of(context).textTheme.labelSmall?.copyWith(color: isDark ? _reconnectAmberDark : _reconnectAmberLight),
                  ),
                ],
              ),
            ),
          Expanded(child: _MessageList(state: state, myUserId: myUserId, scrollController: _scrollController, controller: controller)),
          _Composer(controller: _inputController, onSend: _send, connected: state.connectionStatus == ChatConnectionStatus.connected),
        ],
      ),
    );
  }
}

class _MessageList extends StatelessWidget {
  const _MessageList({required this.state, required this.myUserId, required this.scrollController, required this.controller});

  final ChatState state;
  final String? myUserId;
  final ScrollController scrollController;
  final ChatController controller;

  @override
  Widget build(BuildContext context) {
    if (state.isLoadingHistory) {
      return const Center(child: CircularProgressIndicator());
    }

    if (state.historyFailure != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(state.historyFailure!.message, textAlign: TextAlign.center),
              const SizedBox(height: 12),
              OutlinedButton(onPressed: controller.retryLoadHistory, child: const Text('Retry')),
            ],
          ),
        ),
      );
    }

    if (state.messages.isEmpty && state.outbox.isEmpty) {
      final theme = Theme.of(context);
      return Center(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 44),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.chat_bubble_outline, size: 28, color: theme.colorScheme.outline),
              const SizedBox(height: 12),
              Text(
                'No messages yet. Say hello!',
                style: theme.textTheme.bodySmall?.copyWith(color: theme.textTheme.bodySmall!.color!.withValues(alpha: 0.6)),
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      );
    }

    final lastMineIndex = state.messages.lastIndexWhere((m) => m.senderUserId == myUserId);

    return ListView(
      controller: scrollController,
      padding: const EdgeInsets.all(16),
      children: [
        if (state.isLoadingMoreHistory) const Padding(padding: EdgeInsets.only(bottom: 8), child: Center(child: CircularProgressIndicator())),
        for (var i = 0; i < state.messages.length; i++)
          _MessageBubble(
            message: state.messages[i],
            isMine: state.messages[i].senderUserId == myUserId,
            showReadReceipt: i == lastMineIndex && state.otherLastReadMessageId == state.messages[i].id,
          ),
        for (final item in state.outbox) _OutboxBubble(item: item),
      ],
    );
  }
}

class _MessageBubble extends StatelessWidget {
  const _MessageBubble({required this.message, required this.isMine, required this.showReadReceipt});

  final ChatMessage message;
  final bool isMine;
  final bool showReadReceipt;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final divider = theme.colorScheme.outlineVariant;
    final captionColor = theme.textTheme.bodySmall?.color?.withValues(alpha: 0.4);

    return Align(
      alignment: isMine ? Alignment.centerRight : Alignment.centerLeft,
      child: Column(
        crossAxisAlignment: isMine ? CrossAxisAlignment.end : CrossAxisAlignment.start,
        children: [
          Container(
            margin: const EdgeInsets.only(top: 4),
            padding: const EdgeInsets.symmetric(horizontal: 13, vertical: 10),
            constraints: BoxConstraints(maxWidth: MediaQuery.of(context).size.width * 0.75),
            decoration: BoxDecoration(
              color: isMine ? theme.colorScheme.primary : theme.colorScheme.surfaceContainerHighest,
              border: isMine ? null : Border.all(color: divider),
            ),
            child: Text(message.content, style: theme.textTheme.bodySmall?.copyWith(color: isMine ? theme.colorScheme.onPrimary : null)),
          ),
          Padding(
            padding: const EdgeInsets.only(top: 4),
            child: Text(
              showReadReceipt ? '${_formatTime(message.sentAtUtc)} · Read' : _formatTime(message.sentAtUtc),
              style: theme.textTheme.labelSmall?.copyWith(color: captionColor),
            ),
          ),
        ],
      ),
    );
  }
}

class _OutboxBubble extends StatelessWidget {
  const _OutboxBubble({required this.item});

  final OutboxMessage item;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Align(
      alignment: Alignment.centerRight,
      child: Opacity(
        opacity: 0.5,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Container(
              margin: const EdgeInsets.only(top: 4),
              padding: const EdgeInsets.symmetric(horizontal: 13, vertical: 10),
              constraints: BoxConstraints(maxWidth: MediaQuery.of(context).size.width * 0.75),
              decoration: BoxDecoration(color: theme.colorScheme.primary),
              child: Text(item.content, style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onPrimary)),
            ),
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text('Sending…', style: theme.textTheme.labelSmall?.copyWith(color: theme.textTheme.bodySmall?.color?.withValues(alpha: 0.4))),
            ),
          ],
        ),
      ),
    );
  }
}

class _Composer extends StatelessWidget {
  const _Composer({required this.controller, required this.onSend, required this.connected});

  final TextEditingController controller;
  final VoidCallback onSend;
  final bool connected;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SafeArea(
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        decoration: BoxDecoration(border: Border(top: BorderSide(color: theme.colorScheme.outlineVariant))),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            Expanded(
              child: TextField(
                controller: controller,
                textInputAction: TextInputAction.send,
                onSubmitted: (_) => onSend(),
                decoration: const InputDecoration(hintText: 'Type a message'),
              ),
            ),
            const SizedBox(width: 10),
            SizedBox(
              width: 40,
              height: 40,
              child: IconButton(
                style: IconButton.styleFrom(
                  backgroundColor: connected ? theme.colorScheme.primary : theme.colorScheme.primary.withValues(alpha: 0.4),
                  foregroundColor: theme.colorScheme.onPrimary,
                  shape: const RoundedRectangleBorder(),
                  padding: EdgeInsets.zero,
                ),
                icon: const Icon(Icons.send, size: 16),
                onPressed: onSend,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
