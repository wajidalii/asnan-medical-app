/// Mirrors the backend's `ChatMessageDto`.
class ChatMessage {
  const ChatMessage({
    required this.id,
    required this.chatConversationId,
    required this.senderUserId,
    required this.content,
    required this.sentAtUtc,
  });

  final String id;
  final String chatConversationId;
  final String senderUserId;
  final String content;
  final DateTime sentAtUtc;

  factory ChatMessage.fromJson(Map<String, dynamic> json) => ChatMessage(
        id: json['id'] as String,
        chatConversationId: json['chatConversationId'] as String,
        senderUserId: json['senderUserId'] as String,
        content: json['content'] as String,
        sentAtUtc: DateTime.parse(json['sentAtUtc'] as String),
      );
}
