import 'doctors_failure.dart';

sealed class DoctorsResult<T> {
  const DoctorsResult();
}

class DoctorsSuccess<T> extends DoctorsResult<T> {
  const DoctorsSuccess(this.value);

  final T value;
}

class DoctorsError<T> extends DoctorsResult<T> {
  const DoctorsError(this.failure);

  final DoctorsFailure failure;
}
