import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/chat_failure.dart';
import '../domain/chat_message_page.dart';
import '../domain/chat_result.dart';
import '../domain/read_status.dart';
import 'chat_api.dart';

class ChatRepository {
  ChatRepository(this._api);

  final ChatApi _api;

  Future<ChatResult<ChatMessagePage>> getMessages(String conversationId, {DateTime? before, int pageSize = 20}) =>
      _guard(() => _api.getMessages(conversationId, before: before, pageSize: pageSize));

  Future<ChatResult<ReadStatus>> getReadStatus(String conversationId) => _guard(() => _api.getReadStatus(conversationId));

  Future<ChatResult<T>> _guard<T>(Future<T> Function() action) async {
    try {
      return ChatSuccess(await action());
    } on DioException catch (e) {
      return ChatError(ChatFailure.fromDioException(e));
    }
  }
}

final chatRepositoryProvider = Provider<ChatRepository>((ref) => ChatRepository(ref.watch(chatApiProvider)));
