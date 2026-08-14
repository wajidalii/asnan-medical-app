/// Mirrors the backend's `ReadStatusDto`.
class ReadStatus {
  const ReadStatus({required this.chatConversationId, this.lastReadMessageId, this.lastReadAtUtc, required this.unreadCount});

  final String chatConversationId;
  final String? lastReadMessageId;
  final DateTime? lastReadAtUtc;
  final int unreadCount;

  factory ReadStatus.fromJson(Map<String, dynamic> json) => ReadStatus(
        chatConversationId: json['chatConversationId'] as String,
        lastReadMessageId: json['lastReadMessageId'] as String?,
        lastReadAtUtc: json['lastReadAtUtc'] == null ? null : DateTime.parse(json['lastReadAtUtc'] as String),
        unreadCount: json['unreadCount'] as int,
      );
}
