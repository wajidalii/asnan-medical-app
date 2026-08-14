import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/storage/secure_storage_service.dart';
import '../data/sessions_repository.dart';
import '../domain/sessions_result.dart';
import 'sessions_state.dart';

class SessionsController extends Notifier<SessionsState> {
  @override
  SessionsState build() => const SessionsState();

  Future<void> load() async {
    state = state.copyWith(isLoading: true, failure: null);

    final currentDeviceId = await ref.read(secureStorageProvider).readDeviceId();
    final result = await ref.read(sessionsRepositoryProvider).getSessions();

    switch (result) {
      case SessionsSuccess(:final value):
        state = state.copyWith(isLoading: false, sessions: value, currentDeviceId: currentDeviceId);
      case SessionsError(:final failure):
        state = state.copyWith(isLoading: false, failure: failure);
    }
  }

  Future<void> retry() => load();

  /// "Log out this [other] device" only — the caller's own current session
  /// goes through AuthController.logout() instead (see SessionsScreen),
  /// since that also clears local session state and routes to login.
  /// Returns the raw result so the screen can show the exact failure
  /// message without a dedicated state field for it.
  Future<SessionsResult<void>> revokeSession(String sessionId) async {
    state = state.copyWith(revokingSessionId: sessionId);

    final result = await ref.read(sessionsRepositoryProvider).revokeSession(sessionId);

    switch (result) {
      case SessionsSuccess():
        state = state.copyWith(
          revokingSessionId: null,
          sessions: state.sessions.where((s) => s.id != sessionId).toList(),
        );
      case SessionsError():
        state = state.copyWith(revokingSessionId: null);
    }

    return result;
  }
}

final sessionsControllerProvider = NotifierProvider<SessionsController, SessionsState>(SessionsController.new);
