/// A locally-composed message not yet confirmed sent — issue #29's offline
/// outbox. Purely client-side (never sent to, or returned by, the backend);
/// removed the moment its SendMessage hub invocation resolves successfully
/// — the authoritative copy then arrives normally via the ReceiveMessage
/// broadcast, so this and ChatState.messages never both hold the same
/// message at once (see ChatController.sendMessage's doc comment for the
/// known limitation this implies).
class OutboxMessage {
  const OutboxMessage({required this.localId, required this.content, required this.createdAtUtc, this.failed = false});

  final String localId;
  final String content;
  final DateTime createdAtUtc;
  final bool failed;

  OutboxMessage copyWith({bool? failed}) =>
      OutboxMessage(localId: localId, content: content, createdAtUtc: createdAtUtc, failed: failed ?? this.failed);
}
