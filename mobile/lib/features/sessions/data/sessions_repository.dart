import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../domain/session_summary.dart';
import '../domain/sessions_failure.dart';
import '../domain/sessions_result.dart';
import 'sessions_api.dart';

class SessionsRepository {
  SessionsRepository(this._api);

  final SessionsApi _api;

  Future<SessionsResult<List<SessionSummary>>> getSessions() => _guard(() => _api.getSessions());

  Future<SessionsResult<void>> revokeSession(String sessionId) => _guard(() => _api.revokeSession(sessionId));

  Future<SessionsResult<T>> _guard<T>(Future<T> Function() action) async {
    try {
      return SessionsSuccess(await action());
    } on DioException catch (e) {
      return SessionsError(SessionsFailure.fromDioException(e));
    }
  }
}

final sessionsRepositoryProvider = Provider<SessionsRepository>((ref) => SessionsRepository(ref.watch(sessionsApiProvider)));
