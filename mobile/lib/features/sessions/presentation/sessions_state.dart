import '../domain/session_summary.dart';
import '../domain/sessions_failure.dart';

class _Unset {
  const _Unset();
}

const _unset = _Unset();

class SessionsState {
  const SessionsState({
    this.isLoading = true,
    this.failure,
    this.sessions = const [],
    this.currentDeviceId,
    this.revokingSessionId,
  });

  final bool isLoading;
  final SessionsFailure? failure;
  final List<SessionSummary> sessions;

  /// Null until read from local storage — no row can be identified as "this device" until then.
  final String? currentDeviceId;

  /// The id of the session currently being revoked, if any — disables just that row's action instead of the whole screen.
  final String? revokingSessionId;

  SessionsState copyWith({
    bool? isLoading,
    Object? failure = _unset,
    List<SessionSummary>? sessions,
    String? currentDeviceId,
    Object? revokingSessionId = _unset,
  }) {
    return SessionsState(
      isLoading: isLoading ?? this.isLoading,
      failure: identical(failure, _unset) ? this.failure : failure as SessionsFailure?,
      sessions: sessions ?? this.sessions,
      currentDeviceId: currentDeviceId ?? this.currentDeviceId,
      revokingSessionId: identical(revokingSessionId, _unset) ? this.revokingSessionId : revokingSessionId as String?,
    );
  }
}
