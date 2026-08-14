import 'sessions_failure.dart';

sealed class SessionsResult<T> {
  const SessionsResult();
}

class SessionsSuccess<T> extends SessionsResult<T> {
  const SessionsSuccess(this.value);

  final T value;
}

class SessionsError<T> extends SessionsResult<T> {
  const SessionsError(this.failure);

  final SessionsFailure failure;
}
