import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/current_user.dart';
import '../domain/chat_connection_status.dart';
import '../domain/chat_message.dart';
import '../domain/outbox_message.dart';
import 'chat_controller.dart';
import 'chat_state.dart';

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
              color: Theme.of(context).colorScheme.secondaryContainer,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Text(
                _connectionLabel(state.connectionStatus),
                style: TextStyle(color: Theme.of(context).colorScheme.onSecondaryContainer),
              ),
            ),
          Expanded(child: _MessageList(state: state, myUserId: myUserId, scrollController: _scrollController, controller: controller)),
          _Composer(controller: _inputController, onSend: _send),
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
              FilledButton(onPressed: controller.retryLoadHistory, child: const Text('Retry')),
            ],
          ),
        ),
      );
    }

    if (state.messages.isEmpty && state.outbox.isEmpty) {
      return const Center(child: Text('No messages yet. Say hello!'));
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
    final colorScheme = Theme.of(context).colorScheme;
    return Align(
      alignment: isMine ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 4),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        constraints: BoxConstraints(maxWidth: MediaQuery.of(context).size.width * 0.75),
        decoration: BoxDecoration(
          color: isMine ? colorScheme.primaryContainer : colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(12),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.end,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(message.content),
            const SizedBox(height: 2),
            Text(
              showReadReceipt ? '${_formatTime(message.sentAtUtc)} · Read' : _formatTime(message.sentAtUtc),
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ],
        ),
      ),
    );
  }
}

class _OutboxBubble extends StatelessWidget {
  const _OutboxBubble({required this.item});

  final OutboxMessage item;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.centerRight,
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 4),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        constraints: BoxConstraints(maxWidth: MediaQuery.of(context).size.width * 0.75),
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.primaryContainer.withValues(alpha: 0.5),
          borderRadius: BorderRadius.circular(12),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.end,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(item.content),
            const SizedBox(height: 2),
            Text('Sending…', style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      ),
    );
  }
}

class _Composer extends StatelessWidget {
  const _Composer({required this.controller, required this.onSend});

  final TextEditingController controller;
  final VoidCallback onSend;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(8),
        child: Row(
          children: [
            Expanded(
              child: TextField(
                controller: controller,
                textInputAction: TextInputAction.send,
                onSubmitted: (_) => onSend(),
                decoration: const InputDecoration(hintText: 'Type a message', border: OutlineInputBorder()),
              ),
            ),
            const SizedBox(width: 8),
            IconButton.filled(icon: const Icon(Icons.send), onPressed: onSend),
          ],
        ),
      ),
    );
  }
}
