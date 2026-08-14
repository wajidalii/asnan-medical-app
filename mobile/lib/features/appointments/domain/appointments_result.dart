import 'appointments_failure.dart';

sealed class AppointmentsResult<T> {
  const AppointmentsResult();
}

class AppointmentsSuccess<T> extends AppointmentsResult<T> {
  const AppointmentsSuccess(this.value);

  final T value;
}

class AppointmentsError<T> extends AppointmentsResult<T> {
  const AppointmentsError(this.failure);

  final AppointmentsFailure failure;
}
