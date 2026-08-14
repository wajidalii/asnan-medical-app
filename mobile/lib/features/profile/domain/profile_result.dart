import 'profile_failure.dart';

sealed class ProfileResult<T> {
  const ProfileResult();
}

class ProfileSuccess<T> extends ProfileResult<T> {
  const ProfileSuccess(this.value);

  final T value;
}

class ProfileError<T> extends ProfileResult<T> {
  const ProfileError(this.failure);

  final ProfileFailure failure;
}
