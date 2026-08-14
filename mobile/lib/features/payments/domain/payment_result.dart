import 'payment_failure.dart';

sealed class PaymentResult<T> {
  const PaymentResult();
}

class PaymentSuccess<T> extends PaymentResult<T> {
  const PaymentSuccess(this.value);

  final T value;
}

class PaymentError<T> extends PaymentResult<T> {
  const PaymentError(this.failure);

  final PaymentFailure failure;
}
