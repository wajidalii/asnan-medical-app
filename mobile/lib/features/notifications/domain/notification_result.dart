import 'notification_failure.dart';

sealed class NotificationResult<T> {
  const NotificationResult();
}

class NotificationSuccess<T> extends NotificationResult<T> {
  const NotificationSuccess(this.value);

  final T value;
}

class NotificationError<T> extends NotificationResult<T> {
  const NotificationError(this.failure);

  final NotificationFailure failure;
}
