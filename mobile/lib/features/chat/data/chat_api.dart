import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/dio_client.dart';
import '../domain/chat_message_page.dart';
import '../domain/read_status.dart';

/// REST side of chat (history + read status) — sending happens over the hub, see ChatHubClient.
class ChatApi {
  ChatApi(this._dio);

  final Dio _dio;

  Future<ChatMessagePage> getMessages(String conversationId, {DateTime? before, int pageSize = 20}) async {
    final response = await _dio.get<Map<String, dynamic>>(
      '/chat/conversations/$conversationId/messages',
      queryParameters: {
        if (before != null) 'before': before.toUtc().toIso8601String(),
        'pageSize': pageSize,
      },
    );
    return ChatMessagePage.fromJson(response.data!);
  }

  Future<ReadStatus> getReadStatus(String conversationId) async {
    final response = await _dio.get<Map<String, dynamic>>('/chat/conversations/$conversationId/read-status');
    return ReadStatus.fromJson(response.data!);
  }
}

final chatApiProvider = Provider<ChatApi>((ref) => ChatApi(ref.watch(dioProvider)));
