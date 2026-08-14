import 'chat_message.dart';

/// Mirrors the backend's `ChatMessagePageDto` — oldest-first, per the backend's own ordering.
class ChatMessagePage {
  const ChatMessagePage({required this.messages, required this.nextBeforeCursor, required this.hasMore});

  final List<ChatMessage> messages;
  final DateTime? nextBeforeCursor;
  final bool hasMore;

  factory ChatMessagePage.fromJson(Map<String, dynamic> json) => ChatMessagePage(
        messages: (json['messages'] as List<dynamic>).map((e) => ChatMessage.fromJson(e as Map<String, dynamic>)).toList(),
        nextBeforeCursor: json['nextBeforeCursor'] == null ? null : DateTime.parse(json['nextBeforeCursor'] as String),
        hasMore: json['hasMore'] as bool,
      );
}
