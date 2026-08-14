import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/dio_client.dart';
import '../domain/session_summary.dart';

class SessionsApi {
  SessionsApi(this._dio);

  final Dio _dio;

  Future<List<SessionSummary>> getSessions() async {
    final response = await _dio.get<List<dynamic>>('/auth/sessions');
    return response.data!.map((e) => SessionSummary.fromJson((e as Map).cast<String, dynamic>())).toList();
  }

  /// "Log out this [other] device" — distinct from AuthApi.logout (the caller's own current session) and AuthApi.logoutAll (every session).
  Future<void> revokeSession(String sessionId) => _dio.delete<void>('/auth/sessions/$sessionId');
}

final sessionsApiProvider = Provider<SessionsApi>((ref) => SessionsApi(ref.watch(dioProvider)));
