import 'booking_failure.dart';

sealed class BookingResult<T> {
  const BookingResult();
}

class BookingSuccess<T> extends BookingResult<T> {
  const BookingSuccess(this.value);

  final T value;
}

class BookingError<T> extends BookingResult<T> {
  const BookingError(this.failure);

  final BookingFailure failure;
}
