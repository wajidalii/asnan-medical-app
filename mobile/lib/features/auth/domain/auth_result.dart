import 'auth_failure.dart';

sealed class AuthResult<T> {
  const AuthResult();
}

class AuthSuccess<T> extends AuthResult<T> {
  const AuthSuccess(this.value);

  final T value;
}

class AuthError<T> extends AuthResult<T> {
  const AuthError(this.failure);

  final AuthFailure failure;
}
